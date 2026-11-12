//! Consults: what the doctor found and decided for a visit.

use axum::Router;
use axum::http::{HeaderValue, header};
use axum::response::{IntoResponse, Response};
use axum::routing::{get, post};
use serde::{Deserialize, Serialize};
use sqlx::PgConnection;
use uuid::Uuid;

use crate::auth::Staff;
use crate::domain::{Consult, Diagnosis, DiagnosisCategory, Order, OrderStatus, Patient};
use crate::error::{ApiError, ApiResult, OrNotFound};
use crate::http::{Created, Ctx, Json, NoContent, Req, Rules, Validate};
use crate::pdf::{self, ConsultPdfContent, ConsultPdfDiagnosis};
use crate::state::AppState;
use crate::time::Ts;

/// New consult, its diagnoses, and any prescriptions to record together.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateRequest {
    #[serde(default)]
    pub visit_id: i32,
    pub past_medical_history: Option<String>,
    pub consultation: Option<String>,
    pub treatment_plan: Option<String>,
    pub referred_for: Option<String>,
    pub referral_notes: Option<String>,
    pub remarks: Option<String>,
    #[serde(default)]
    pub diagnoses: Option<Vec<DiagnosisInput>>,
    #[serde(default)]
    pub orders: Option<Vec<OrderInput>>,
}

/// Diagnosis captured with a new consult.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DiagnosisInput {
    #[serde(default)]
    pub details: String,
    pub category: Option<DiagnosisCategory>,
}

/// Prescription inserted with a new consult. Status is always pending.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct OrderInput {
    #[serde(default)]
    pub medication_brand_id: i32,
    #[serde(default)]
    pub quantity: i32,
    pub dosage_instructions: Option<String>,
    pub remarks: Option<String>,
}

impl Validate for CreateRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("visitId", self.visit_id, 0);
        let diagnoses = self.diagnoses.as_deref().unwrap_or_default();
        v.not_empty_list("diagnoses", diagnoses);
        for (index, diagnosis) in diagnoses.iter().enumerate() {
            v.not_empty(&format!("diagnoses[{index}].details"), &diagnosis.details);
        }
        for (index, order) in self.orders.iter().flatten().enumerate() {
            v.greater_than(&format!("orders[{index}].medicationBrandId"), order.medication_brand_id, 0);
            v.greater_than(&format!("orders[{index}].quantity"), order.quantity, 0);
        }
    }
}

/// Consult recorded by the current doctor, including its diagnoses.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ConsultResponse {
    pub id: i32,
    pub visit_id: i32,
    pub date: Ts,
    pub past_medical_history: Option<String>,
    pub consultation: Option<String>,
    pub treatment_plan: Option<String>,
    pub referred_for: Option<String>,
    pub referral_notes: Option<String>,
    pub remarks: Option<String>,
    pub doctor_id: Uuid,
    pub doctor_nickname: String,
    pub diagnoses: Vec<DiagnosisItem>,
}

/// Diagnosis recorded on a consult.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct DiagnosisItem {
    pub id: i32,
    pub consult_id: i32,
    pub details: String,
    pub category: Option<DiagnosisCategory>,
}

impl From<Diagnosis> for DiagnosisItem {
    fn from(diagnosis: Diagnosis) -> Self {
        DiagnosisItem {
            id: diagnosis.id,
            consult_id: diagnosis.consult_id,
            details: diagnosis.details,
            category: diagnosis.category,
        }
    }
}

impl ConsultResponse {
    pub fn from(consult: Consult, doctor_nickname: String, mut diagnoses: Vec<Diagnosis>) -> Self {
        diagnoses.sort_by_key(|diagnosis| diagnosis.id);
        ConsultResponse {
            id: consult.id,
            visit_id: consult.visit_id,
            date: consult.date,
            past_medical_history: consult.past_medical_history,
            consultation: consult.consultation,
            treatment_plan: consult.treatment_plan,
            referred_for: consult.referred_for,
            referral_notes: consult.referral_notes,
            remarks: consult.remarks,
            doctor_id: consult.doctor_id,
            doctor_nickname,
            diagnoses: diagnoses.into_iter().map(Into::into).collect(),
        }
    }
}

/// Record a consult with diagnoses and optional prescriptions.
async fn create(ctx: Ctx, Staff(user): Staff, Req(req): Req<CreateRequest>) -> ApiResult<Created<ConsultResponse>> {
    let mut tx = ctx.begin().await?;
    let visit_exists: bool = sqlx::query_scalar(r#"SELECT EXISTS (SELECT 1 FROM "Visits" WHERE "Id" = $1)"#)
        .bind(req.visit_id)
        .fetch_one(&mut *tx)
        .await?;
    if !visit_exists {
        return Err(ApiError::not_found("Visit was not found."));
    }
    let inputs = req.diagnoses.unwrap_or_default();
    if inputs.is_empty() {
        return Err(ApiError::bad_request("At least one diagnosis is required."));
    }

    let orders = req.orders.unwrap_or_default();
    if !orders.is_empty() {
        if orders.iter().any(|order| order.quantity < 1) {
            return Err(ApiError::bad_request("Order quantity must be at least 1."));
        }
        let mut brand_ids: Vec<i32> = orders.iter().map(|order| order.medication_brand_id).collect();
        brand_ids.sort_unstable();
        brand_ids.dedup();
        let found: i64 = sqlx::query_scalar(r#"SELECT COUNT(*) FROM "MedicationBrands" WHERE "Id" = ANY($1)"#)
            .bind(&brand_ids)
            .fetch_one(&mut *tx)
            .await?;
        if found != brand_ids.len() as i64 {
            return Err(ApiError::bad_request("Medication brand was not found."));
        }
    }
    let mut details = Vec::with_capacity(inputs.len());
    for diagnosis in &inputs {
        let text = diagnosis.details.trim();
        if text.is_empty() {
            return Err(ApiError::bad_request("Diagnosis details are required."));
        }
        details.push((text.to_owned(), diagnosis.category));
    }

    let now = Ts::now();
    let consult = tx
        .insert(Consult {
            id: 0,
            date: now,
            past_medical_history: req.past_medical_history,
            consultation: req.consultation,
            treatment_plan: req.treatment_plan,
            referred_for: req.referred_for,
            referral_notes: req.referral_notes,
            remarks: req.remarks,
            doctor_id: user.id,
            visit_id: req.visit_id,
        })
        .await?;
    let mut diagnoses = Vec::with_capacity(details.len());
    for (details, category) in details {
        diagnoses.push(tx.insert(Diagnosis { id: 0, details, category, consult_id: consult.id }).await?);
    }
    for order in orders {
        tx.insert(Order {
            id: 0,
            consult_id: consult.id,
            medication_brand_id: order.medication_brand_id,
            dosage_instructions: order.dosage_instructions.unwrap_or_default(),
            remarks: order.remarks,
            status: OrderStatus::Pending,
            quantity: order.quantity,
            approved_by_id: None,
            approved_at: None,
            created_at: now,
        })
        .await?;
    }
    let nickname: Option<String> = sqlx::query_scalar(r#"SELECT "Nickname" FROM "StaffUsers" WHERE "Id" = $1"#)
        .bind(consult.doctor_id)
        .fetch_optional(&mut *tx)
        .await?;
    let nickname = nickname.ok_or_else(|| ApiError::not_found("Doctor was not found."))?;
    tx.commit().await?;

    Ok(Created::at(format!("/consults/{}", consult.id), ConsultResponse::from(consult, nickname, diagnoses)))
}

const NOT_FOUND: &str = "Consult was not found.";

/// Identifies a consult.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct IdRequest {
    #[serde(default)]
    pub id: i32,
}

impl Validate for IdRequest {}

/// A consult with the nickname of the doctor who wrote it.
#[derive(sqlx::FromRow)]
struct ConsultRow {
    #[sqlx(flatten)]
    consult: Consult,
    #[sqlx(rename = "DoctorNickname")]
    doctor_nickname: String,
}

const SELECT_CONSULT: &str = r#"
    SELECT c.*, d."Nickname" AS "DoctorNickname"
    FROM "Consults" c
    JOIN "StaffUsers" d ON d."Id" = c."DoctorId""#;

async fn find(conn: &mut PgConnection, id: i32) -> ApiResult<ConsultRow> {
    sqlx::query_as(&format!(r#"{SELECT_CONSULT} WHERE c."Id" = $1"#))
        .bind(id)
        .fetch_optional(conn)
        .await
        .or_not_found(NOT_FOUND)
}

/// The diagnoses of the given consults, in id order.
async fn diagnoses_of(conn: &mut PgConnection, consult_ids: &[i32]) -> ApiResult<Vec<Diagnosis>> {
    Ok(sqlx::query_as(r#"SELECT * FROM "Diagnoses" WHERE "ConsultId" = ANY($1) ORDER BY "Id""#)
        .bind(consult_ids)
        .fetch_all(conn)
        .await?)
}

/// Get a consult.
async fn get_one(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<Json<ConsultResponse>> {
    let mut conn = ctx.db().acquire().await?;
    let row = find(&mut conn, req.id).await?;
    let diagnoses = diagnoses_of(&mut conn, &[row.consult.id]).await?;
    Ok(Json(ConsultResponse::from(row.consult, row.doctor_nickname, diagnoses)))
}

/// Visit whose consults should be listed.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ListByVisitRequest {
    #[serde(default)]
    pub visit_id: i32,
}

impl Validate for ListByVisitRequest {}

/// List consults for a visit, newest first.
async fn list_by_visit(
    ctx: Ctx,
    _: Staff,
    Req(req): Req<ListByVisitRequest>,
) -> ApiResult<Json<Vec<ConsultResponse>>> {
    let mut conn = ctx.db().acquire().await?;
    let visit_exists: bool = sqlx::query_scalar(r#"SELECT EXISTS (SELECT 1 FROM "Visits" WHERE "Id" = $1)"#)
        .bind(req.visit_id)
        .fetch_one(&mut *conn)
        .await?;
    if !visit_exists {
        return Err(ApiError::not_found("Visit was not found."));
    }
    let rows: Vec<ConsultRow> =
        sqlx::query_as(&format!(r#"{SELECT_CONSULT} WHERE c."VisitId" = $1 ORDER BY c."Date" DESC, c."Id" DESC"#))
            .bind(req.visit_id)
            .fetch_all(&mut *conn)
            .await?;
    let ids: Vec<i32> = rows.iter().map(|row| row.consult.id).collect();
    let mut remaining = diagnoses_of(&mut conn, &ids).await?;

    let consults = rows
        .into_iter()
        .map(|row| {
            let (own, others) =
                std::mem::take(&mut remaining).into_iter().partition(|diagnosis| diagnosis.consult_id == row.consult.id);
            remaining = others;
            ConsultResponse::from(row.consult, row.doctor_nickname, own)
        })
        .collect();
    Ok(Json(consults))
}

/// Partial text update. Omitted fields are left unchanged. Diagnoses and orders are not edited.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateRequest {
    #[serde(default)]
    pub id: i32,
    pub past_medical_history: Option<String>,
    pub consultation: Option<String>,
    pub treatment_plan: Option<String>,
    pub referred_for: Option<String>,
    pub referral_notes: Option<String>,
    pub remarks: Option<String>,
}

impl Validate for UpdateRequest {}

/// Update consult text within 24 hours.
async fn update(ctx: Ctx, _: Staff, Req(req): Req<UpdateRequest>) -> ApiResult<Json<ConsultResponse>> {
    let mut tx = ctx.begin().await?;
    let row = find(&mut tx, req.id).await?;
    let before = row.consult;
    if before.is_edit_locked(Ts::now()) {
        return Err(ApiError::locked("Cannot edit consult after 24 hours"));
    }

    let mut consult = before.clone();
    if req.past_medical_history.is_some() {
        consult.past_medical_history = req.past_medical_history;
    }
    if req.consultation.is_some() {
        consult.consultation = req.consultation;
    }
    if req.treatment_plan.is_some() {
        consult.treatment_plan = req.treatment_plan;
    }
    if req.referred_for.is_some() {
        consult.referred_for = req.referred_for;
    }
    if req.referral_notes.is_some() {
        consult.referral_notes = req.referral_notes;
    }
    if req.remarks.is_some() {
        consult.remarks = req.remarks;
    }

    let consult = tx.update(&before, consult).await?;
    let diagnoses = diagnoses_of(&mut tx, &[consult.id]).await?;
    tx.commit().await?;
    Ok(Json(ConsultResponse::from(consult, row.doctor_nickname, diagnoses)))
}

/// Delete a consult and its diagnoses.
async fn delete(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<NoContent> {
    let mut tx = ctx.begin().await?;
    let consult: Consult = sqlx::query_as(r#"SELECT * FROM "Consults" WHERE "Id" = $1"#)
        .bind(req.id)
        .fetch_optional(&mut *tx)
        .await
        .or_not_found(NOT_FOUND)?;
    let has_referrals: bool =
        sqlx::query_scalar(r#"SELECT EXISTS (SELECT 1 FROM "Referrals" WHERE "ConsultId" = $1)"#)
            .bind(consult.id)
            .fetch_one(&mut *tx)
            .await?;
    if has_referrals {
        return Err(ApiError::conflict("Cannot delete a consult that still has referrals."));
    }
    let has_orders: bool = sqlx::query_scalar(r#"SELECT EXISTS (SELECT 1 FROM "Orders" WHERE "ConsultId" = $1)"#)
        .bind(consult.id)
        .fetch_one(&mut *tx)
        .await?;
    if has_orders {
        return Err(ApiError::conflict("Cannot delete a consult that still has orders."));
    }

    // The diagnoses go with the consult through the database's cascade, unlogged as before.
    tx.delete(&consult).await?;
    tx.commit().await?;
    Ok(NoContent)
}

/// A consult with what its report prints about the doctor and the patient.
#[derive(sqlx::FromRow)]
struct ReportRow {
    #[sqlx(flatten)]
    consult: Consult,
    #[sqlx(rename = "DoctorNickname")]
    doctor_nickname: String,
    #[sqlx(rename = "PatientId")]
    patient_id: i32,
    #[sqlx(rename = "PatientName")]
    patient_name: String,
    /// The patient's own village code, or else the code of the village the visit was in.
    #[sqlx(rename = "VillageCode")]
    village_code: String,
}

/// Download a consult as a PDF.
async fn report(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<Response> {
    let mut conn = ctx.db().acquire().await?;
    let row: ReportRow = sqlx::query_as(
        r#"
        SELECT c.*, d."Nickname" AS "DoctorNickname", p."Id" AS "PatientId", p."Name" AS "PatientName",
               COALESCE(home."Code", seen."Code") AS "VillageCode"
        FROM "Consults" c
        JOIN "StaffUsers" d ON d."Id" = c."DoctorId"
        JOIN "Visits" v ON v."Id" = c."VisitId"
        JOIN "Patients" p ON p."Id" = v."PatientId"
        JOIN "VillageCodes" seen ON seen."Id" = v."VillageCodeId"
        LEFT JOIN "VillageCodes" home ON home."Id" = p."VillageCodeId"
        WHERE c."Id" = $1"#,
    )
    .bind(req.id)
    .fetch_optional(&mut *conn)
    .await
    .or_not_found(NOT_FOUND)?;
    let diagnoses = diagnoses_of(&mut conn, &[row.consult.id]).await?;
    drop(conn);

    let consult = row.consult;
    let display_id = Patient::format_display_id(Some(&row.village_code), row.patient_id);
    let safe_id: String = display_id.chars().map(|ch| if ch.is_ascii_alphanumeric() { ch } else { '_' }).collect();
    let file_name = format!("consultation{}_report_{safe_id}.pdf", consult.id);
    let content = ConsultPdfContent {
        patient_display_id: display_id,
        patient_name: row.patient_name,
        doctor_nickname: row.doctor_nickname,
        past_medical_history: consult.past_medical_history,
        consultation: consult.consultation,
        diagnoses: diagnoses
            .into_iter()
            .map(|diagnosis| ConsultPdfDiagnosis {
                category: diagnosis.category.map(|category| category.wire().to_owned()),
                details: diagnosis.details,
            })
            .collect(),
        treatment_plan: consult.treatment_plan,
        referred_for: consult.referred_for,
        referral_notes: consult.referral_notes,
        remarks: consult.remarks,
    };
    let bytes = tokio::task::spawn_blocking(move || pdf::consult_report(&content))
        .await
        .map_err(ApiError::internal)??;

    // The name is made of ASCII letters, digits and underscores, so it needs no quoting.
    let disposition = format!("attachment; filename={file_name}; filename*=UTF-8''{file_name}");
    let disposition = HeaderValue::from_str(&disposition).map_err(ApiError::internal)?;
    Ok((
        [(header::CONTENT_TYPE, HeaderValue::from_static("application/pdf")), (header::CONTENT_DISPOSITION, disposition)],
        bytes,
    )
        .into_response())
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/consults", post(create))
        .route("/consults/{id}", get(get_one).patch(update).delete(delete))
        .route("/consults/{id}/pdf", get(report))
        .route("/visits/{visitId}/consults", get(list_by_visit))
}

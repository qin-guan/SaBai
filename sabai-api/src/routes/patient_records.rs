//! The patient chart for a visit: identity, vitals, consult summaries and prescriptions.

use axum::Router;
use axum::routing::get;
use serde::{Deserialize, Serialize};

use crate::auth::Staff;
use crate::domain::{Decimal, Gender, OrderStatus, Patient, Visit, Vitals};
use crate::error::{ApiError, ApiResult, OrNotFound};
use crate::http::{Ctx, Json, Req, Validate};
use crate::state::AppState;
use crate::time::Ts;

/// Visit whose chart should be loaded. Must be a positive integer.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ChartRequest {
    #[serde(default)]
    pub visit_id: i32,
}

impl Validate for ChartRequest {}

/// Patient chart for one visit: identity, vitals, consult summaries, and prescriptions.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ChartResponse {
    pub patient: PatientItem,
    pub vitals: Option<VitalsItem>,
    pub visit_date: Ts,
    pub consults: Vec<ConsultItem>,
    pub prescriptions: Vec<PrescriptionItem>,
}

/// Patient identity shown on a visit chart.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PatientItem {
    pub id: i32,
    pub name: String,
    pub identification_number: Option<String>,
    pub contact_no: Option<String>,
    pub gender: Gender,
    pub date_of_birth: Ts,
    pub drug_allergy: String,
    pub has_poor_card: bool,
    #[serde(rename = "hasBS2Card")]
    pub has_bs2_card: bool,
    pub has_sabai_card: bool,
    pub to_get_report: bool,
    pub village_code_id: Option<i32>,
    pub village_code: Option<String>,
    pub display_id: String,
    pub filter_string: String,
    pub last_visit_date: Option<Ts>,
    pub last_visit_id: Option<i32>,
}

/// Vitals recorded for the visit, or null when none exist.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct VitalsItem {
    pub id: i32,
    pub visit_id: i32,
    pub height: Option<Decimal>,
    pub weight: Option<Decimal>,
    pub temperature: Option<Decimal>,
    pub systolic: Option<i32>,
    pub diastolic: Option<i32>,
    pub heart_rate: Option<i32>,
    pub hemocue_count: Option<Decimal>,
    pub diabetes_mellitus: Option<bool>,
    pub urine_test: Option<String>,
    pub blood_glucose_non_fasting: Option<Decimal>,
    pub blood_glucose_fasting: Option<Decimal>,
    pub hba1c: Option<Decimal>,
    pub others: Option<String>,
}

impl From<Vitals> for VitalsItem {
    fn from(vitals: Vitals) -> Self {
        VitalsItem {
            id: vitals.id,
            visit_id: vitals.visit_id,
            height: vitals.height,
            weight: vitals.weight,
            temperature: vitals.temperature,
            systolic: vitals.systolic,
            diastolic: vitals.diastolic,
            heart_rate: vitals.heart_rate,
            hemocue_count: vitals.hemocue_count,
            diabetes_mellitus: vitals.diabetes_mellitus,
            urine_test: vitals.urine_test,
            blood_glucose_non_fasting: vitals.blood_glucose_non_fasting,
            blood_glucose_fasting: vitals.blood_glucose_fasting,
            hba1c: vitals.hba1c,
            others: vitals.others,
        }
    }
}

/// Consult summary, including the doctor nickname.
#[derive(Debug, Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
pub struct ConsultItem {
    #[sqlx(rename = "Id")]
    pub id: i32,
    #[sqlx(rename = "Date")]
    pub date: Ts,
    #[sqlx(rename = "DoctorNickname")]
    pub doctor_nickname: String,
    #[sqlx(rename = "ReferredFor")]
    pub referred_for: Option<String>,
}

/// Prescription on a consult of this visit. Notes are the order remarks.
#[derive(Debug, Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
pub struct PrescriptionItem {
    #[sqlx(rename = "ConsultId")]
    pub consult_id: i32,
    #[sqlx(rename = "VisitDate")]
    pub visit_date: Ts,
    #[sqlx(rename = "MedicationName")]
    pub medication_name: String,
    #[sqlx(rename = "Quantity")]
    pub quantity: i32,
    #[sqlx(rename = "Notes")]
    pub notes: Option<String>,
    #[sqlx(rename = "Status")]
    pub status: OrderStatus,
}

/// The order consult summaries are listed in.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ConsultOrder {
    OldestFirst,
    NewestFirst,
}

/// Loads the chart of a visit. The consult workspace and the records view share it and
/// differ only in the order of the consult summaries.
pub async fn chart(ctx: &Ctx, visit_id: i32, order: ConsultOrder) -> ApiResult<ChartResponse> {
    if visit_id < 1 {
        return Err(ApiError::bad_request("visitId must be a positive integer."));
    }

    let mut conn = ctx.db().acquire().await?;
    let visit: Visit = sqlx::query_as(r#"SELECT * FROM "Visits" WHERE "Id" = $1"#)
        .bind(visit_id)
        .fetch_optional(&mut *conn)
        .await
        .or_not_found("Visit was not found.")?;
    let patient: Patient = sqlx::query_as(r#"SELECT * FROM "Patients" WHERE "Id" = $1"#)
        .bind(visit.patient_id)
        .fetch_one(&mut *conn)
        .await?;
    let village_code: Option<String> = sqlx::query_scalar(r#"SELECT "Code" FROM "VillageCodes" WHERE "Id" = $1"#)
        .bind(patient.village_code_id)
        .fetch_optional(&mut *conn)
        .await?;
    let vitals: Option<Vitals> = sqlx::query_as(r#"SELECT * FROM "Vitals" WHERE "VisitId" = $1"#)
        .bind(visit.id)
        .fetch_optional(&mut *conn)
        .await?;

    let direction = match order {
        ConsultOrder::OldestFirst => "ASC",
        ConsultOrder::NewestFirst => "DESC",
    };
    let consults: Vec<ConsultItem> = sqlx::query_as(&format!(
        r#"
        SELECT c."Id", c."Date", d."Nickname" AS "DoctorNickname", c."ReferredFor"
        FROM "Consults" c
        JOIN "StaffUsers" d ON d."Id" = c."DoctorId"
        WHERE c."VisitId" = $1
        ORDER BY c."Date" {direction}, c."Id" {direction}"#
    ))
    .bind(visit.id)
    .fetch_all(&mut *conn)
    .await?;

    let prescriptions: Vec<PrescriptionItem> = sqlx::query_as(
        r#"
        SELECT o."ConsultId", v."Date" AS "VisitDate", b."Name" AS "MedicationName", o."Quantity",
               o."Remarks" AS "Notes", o."Status"
        FROM "Orders" o
        JOIN "Consults" c ON c."Id" = o."ConsultId"
        JOIN "Visits" v ON v."Id" = c."VisitId"
        JOIN "MedicationBrands" b ON b."Id" = o."MedicationBrandId"
        WHERE c."VisitId" = $1
        ORDER BY c."Date", o."Id""#,
    )
    .bind(visit.id)
    .fetch_all(&mut *conn)
    .await?;

    let last_visit: Option<(i32, Ts)> = sqlx::query_as(
        r#"SELECT "Id", "Date" FROM "Visits" WHERE "PatientId" = $1 ORDER BY "Date" DESC, "Id" DESC LIMIT 1"#,
    )
    .bind(visit.patient_id)
    .fetch_optional(&mut *conn)
    .await?;

    let code = village_code.as_deref();
    Ok(ChartResponse {
        patient: PatientItem {
            id: patient.id,
            display_id: Patient::format_display_id(code, patient.id),
            filter_string: Patient::format_filter_string(code, patient.id, patient.contact_no.as_deref(), &patient.name),
            name: patient.name,
            identification_number: patient.identification_number,
            contact_no: patient.contact_no,
            gender: patient.gender,
            date_of_birth: patient.date_of_birth,
            drug_allergy: patient.drug_allergy,
            has_poor_card: patient.has_poor_card,
            has_bs2_card: patient.has_bs2_card,
            has_sabai_card: patient.has_sabai_card,
            to_get_report: patient.to_get_report,
            village_code_id: patient.village_code_id,
            village_code,
            last_visit_date: last_visit.map(|(_, date)| date),
            last_visit_id: last_visit.map(|(id, _)| id),
        },
        vitals: vitals.map(Into::into),
        visit_date: visit.date,
        consults,
        prescriptions,
    })
}

/// Load the patient chart for a visit.
async fn patient_records(ctx: Ctx, _: Staff, Req(req): Req<ChartRequest>) -> ApiResult<Json<ChartResponse>> {
    Ok(Json(chart(&ctx, req.visit_id, ConsultOrder::NewestFirst).await?))
}

pub fn router() -> Router<AppState> {
    Router::new().route("/patient-records", get(patient_records))
}

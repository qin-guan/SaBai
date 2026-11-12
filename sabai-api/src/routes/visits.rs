//! Visits: one trip of a patient through the clinic.

use axum::Router;
use axum::routing::get;
use chrono::Duration;
use serde::{Deserialize, Serialize};
use sqlx::PgConnection;

use crate::auth::Staff;
use crate::domain::{Patient, VillageCode, Visit};
use crate::error::{ApiError, ApiResult, OrNotFound};
use crate::http::{Created, Ctx, Json, NoContent, Req, Rules, Validate};
use crate::state::AppState;
use crate::time::Ts;

const NOT_FOUND: &str = "Visit was not found.";
const PATIENT_NOT_FOUND: &str = "Patient was not found.";
const VILLAGE_NOT_FOUND: &str = "Village code was not found.";

/// A patient registered twice within this window is probably a double submission.
const DUPLICATE_WINDOW_MINUTES: i64 = 1;

/// Visits joined to their patient and village; add a filter and an order.
pub(crate) const SELECT_VISITS: &str = r#"
    SELECT v."Id", v."Date", v."PatientId", p."Name" AS "PatientName", v."VillageCodeId",
           vc."Code" AS "VillageCode", vc."Name" AS "VillageName", vc."ColorHex" AS "VillageColorHex", v."Status"
    FROM "Visits" v
    JOIN "Patients" p ON p."Id" = v."PatientId"
    JOIN "VillageCodes" vc ON vc."Id" = v."VillageCodeId"
"#;

/// A visit joined to its patient and village.
#[derive(Debug, Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
#[sqlx(rename_all = "PascalCase")]
pub struct VisitResponse {
    pub id: i32,
    pub date: Ts,
    pub patient_id: i32,
    pub patient_name: String,
    pub village_code_id: i32,
    pub village_code: String,
    pub village_name: String,
    pub village_color_hex: String,
    pub status: String,
}

/// Identifies a visit.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct IdRequest {
    #[serde(default)]
    pub id: i32,
}

impl Validate for IdRequest {}

async fn find(conn: &mut PgConnection, id: i32) -> ApiResult<Visit> {
    sqlx::query_as(r#"SELECT * FROM "Visits" WHERE "Id" = $1"#).bind(id).fetch_optional(conn).await.or_not_found(NOT_FOUND)
}

async fn load(conn: &mut PgConnection, id: i32) -> ApiResult<VisitResponse> {
    sqlx::query_as(&format!(r#"{SELECT_VISITS} WHERE v."Id" = $1"#))
        .bind(id)
        .fetch_optional(conn)
        .await
        .or_not_found(NOT_FOUND)
}

async fn find_patient(conn: &mut PgConnection, id: i32) -> ApiResult<Patient> {
    sqlx::query_as(r#"SELECT * FROM "Patients" WHERE "Id" = $1"#)
        .bind(id)
        .fetch_optional(conn)
        .await
        .or_not_found(PATIENT_NOT_FOUND)
}

async fn find_village(conn: &mut PgConnection, id: i32) -> ApiResult<VillageCode> {
    sqlx::query_as(r#"SELECT * FROM "VillageCodes" WHERE "Id" = $1"#)
        .bind(id)
        .fetch_optional(conn)
        .await
        .or_not_found(VILLAGE_NOT_FOUND)
}

/// Optional patient filter for the visit list.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ListRequest {
    pub patient_id: Option<i32>,
}

impl Validate for ListRequest {}

/// Visits joined to patient and village, newest first.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ListResponse {
    pub visits: Vec<VisitResponse>,
}

/// List visits.
async fn list(ctx: Ctx, _: Staff, Req(req): Req<ListRequest>) -> ApiResult<Json<ListResponse>> {
    let visits = sqlx::query_as(&format!(
        r#"{SELECT_VISITS} WHERE ($1::int IS NULL OR v."PatientId" = $1) ORDER BY v."Date" DESC, v."Id" DESC"#
    ))
    .bind(req.patient_id)
    .fetch_all(ctx.db())
    .await?;
    Ok(Json(ListResponse { visits }))
}

/// Get a visit.
async fn get_one(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<Json<VisitResponse>> {
    let mut conn = ctx.db().acquire().await?;
    Ok(Json(load(&mut conn, req.id).await?))
}

/// Payload for opening a visit. Date and status default when omitted.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateRequest {
    #[serde(default)]
    pub patient_id: i32,
    #[serde(default)]
    pub village_code_id: i32,
    pub date: Option<Ts>,
    pub status: Option<String>,
}

impl Validate for CreateRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("patientId", self.patient_id, 0);
        v.greater_than("villageCodeId", self.village_code_id, 0);
        if self.status.is_some() {
            v.not_empty("status", &self.status).max_len("status", &self.status, 100);
        }
    }
}

/// Create a visit.
async fn create(ctx: Ctx, _: Staff, Req(req): Req<CreateRequest>) -> ApiResult<Created<VisitResponse>> {
    let mut tx = ctx.begin().await?;
    let patient = find_patient(&mut tx, req.patient_id).await?;
    let village = find_village(&mut tx, req.village_code_id).await?;

    let status = match req.status.as_deref().map(str::trim) {
        Some(status) if !status.is_empty() => status.to_owned(),
        _ => Visit::STARTED_STATUS.to_owned(),
    };
    let visit = tx
        .insert(Visit {
            id: 0,
            patient_id: patient.id,
            village_code_id: village.id,
            date: req.date.unwrap_or_else(Ts::now),
            status,
        })
        .await?;
    tx.commit().await?;

    let response = VisitResponse {
        id: visit.id,
        date: visit.date,
        patient_id: patient.id,
        patient_name: patient.name,
        village_code_id: village.id,
        village_code: village.code,
        village_name: village.name,
        village_color_hex: village.color_hex,
        status: visit.status,
    };
    Ok(Created::at(format!("/visits/{}", response.id), response))
}

/// Partial visit update. Omitted fields are left unchanged.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateRequest {
    #[serde(default)]
    pub id: i32,
    pub patient_id: Option<i32>,
    pub village_code_id: Option<i32>,
    pub date: Option<Ts>,
    pub status: Option<String>,
}

impl Validate for UpdateRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("patientId", self.patient_id, 0);
        v.greater_than("villageCodeId", self.village_code_id, 0);
        if self.status.is_some() {
            v.not_empty("status", &self.status).max_len("status", &self.status, 100);
        }
    }
}

/// Update a visit.
async fn update(ctx: Ctx, _: Staff, Req(req): Req<UpdateRequest>) -> ApiResult<Json<VisitResponse>> {
    let mut tx = ctx.begin().await?;
    let before = find(&mut tx, req.id).await?;
    let mut visit = before.clone();

    if let Some(patient_id) = req.patient_id {
        find_patient(&mut tx, patient_id).await?;
        visit.patient_id = patient_id;
    }
    if let Some(village_code_id) = req.village_code_id {
        find_village(&mut tx, village_code_id).await?;
        visit.village_code_id = village_code_id;
    }
    if let Some(date) = req.date {
        visit.date = date;
    }
    if let Some(status) = &req.status {
        visit.status = status.trim().to_owned();
    }

    let visit = tx.update(&before, visit).await?;
    let response = load(&mut tx, visit.id).await?;
    tx.commit().await?;
    Ok(Json(response))
}

/// Delete a visit.
async fn delete(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<NoContent> {
    let mut tx = ctx.begin().await?;
    let visit = find(&mut tx, req.id).await?;
    let has_consult: bool = sqlx::query_scalar(r#"SELECT EXISTS (SELECT 1 FROM "Consults" WHERE "VisitId" = $1)"#)
        .bind(visit.id)
        .fetch_one(&mut *tx)
        .await?;
    if has_consult {
        return Err(ApiError::conflict("A consult still references this visit."));
    }
    tx.delete(&visit).await?;
    tx.commit().await?;
    Ok(NoContent)
}

/// Identifies the patient to check for a just-created visit.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct HasRecentRequest {
    #[serde(default)]
    pub patient_id: i32,
}

impl Validate for HasRecentRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("patientId", self.patient_id, 0);
    }
}

/// Whether the patient already had a visit opened within the duplicate window.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct HasRecentResponse {
    pub has_recent_visit: bool,
    pub existing_visit_date: Option<Ts>,
}

/// Check whether a patient had a visit opened in the last minute.
async fn has_recent(ctx: Ctx, _: Staff, Req(req): Req<HasRecentRequest>) -> ApiResult<Json<HasRecentResponse>> {
    let window_start = Ts::now() - Duration::minutes(DUPLICATE_WINDOW_MINUTES);
    let existing_visit_date: Option<Ts> = sqlx::query_scalar(
        r#"SELECT "Date" FROM "Visits" WHERE "PatientId" = $1 AND "Date" >= $2 ORDER BY "Date" DESC LIMIT 1"#,
    )
    .bind(req.patient_id)
    .bind(window_start)
    .fetch_optional(ctx.db())
    .await?;
    Ok(Json(HasRecentResponse { has_recent_visit: existing_visit_date.is_some(), existing_visit_date }))
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/visits", get(list).post(create))
        .route("/visits/recent", get(has_recent))
        .route("/visits/{id}", get(get_one).patch(update).delete(delete))
}

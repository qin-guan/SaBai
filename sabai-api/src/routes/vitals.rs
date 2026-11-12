//! Vitals: the measurements taken for a visit. One record per visit.

use axum::Router;
use axum::routing::{get, post};
use serde::{Deserialize, Serialize};
use sqlx::PgConnection;

use crate::auth::Staff;
use crate::domain::{Decimal, Vitals};
use crate::error::{ApiError, ApiResult, OrNotFound};
use crate::http::{Created, Ctx, Json, NoContent, Req, Rules, Validate};
use crate::state::AppState;

const NOT_FOUND: &str = "Vitals were not found.";
const ALREADY_EXISTS: &str = "Vitals already exist for this visit.";

/// Blank text is no value.
pub(super) fn free_text(value: Option<&str>) -> Option<String> {
    value.map(str::trim).filter(|value| !value.is_empty()).map(str::to_owned)
}

/// Whether the insert hit a unique index: another request created the one record a
/// visit (or vitals record) may have in the meantime.
pub(super) fn is_duplicate(error: &sqlx::Error) -> bool {
    error.as_database_error().and_then(|error| error.code()).is_some_and(|code| code == "23505")
}

/// Turns a unique-index violation into a 409 with the endpoint's own message.
pub(super) fn conflict_if_duplicate(error: sqlx::Error, message: &str) -> ApiError {
    if is_duplicate(&error) { ApiError::conflict(message) } else { error.into() }
}

pub(super) async fn exists(
    conn: &mut PgConnection,
    table: &str,
    column: &str,
    value: i32,
) -> Result<bool, sqlx::Error> {
    sqlx::query_scalar(&format!(r#"SELECT EXISTS (SELECT 1 FROM "{table}" WHERE "{column}" = $1)"#))
        .bind(value)
        .fetch_one(conn)
        .await
}

/// A request whose record is named by its own id in the route (`/vitals/{id}`).
#[derive(Debug, Deserialize)]
#[serde(transparent)]
pub(super) struct ById<T>(pub T);

/// A request whose record is named by its visit in the route (`/visits/{visitId}/vitals`).
#[derive(Debug, Deserialize)]
#[serde(transparent)]
pub(super) struct ByVisit<T>(pub T);

/// A request whose record is named by its vitals record in the route (`/vitals/{vitalsId}/puberty`).
#[derive(Debug, Deserialize)]
#[serde(transparent)]
pub(super) struct ByVitals<T>(pub T);

/// Identifies a record by id.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub(super) struct IdRequest {
    #[serde(default)]
    pub id: i32,
}

impl Validate for IdRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("id", self.id, 0);
    }
}

/// Identifies the visit whose record is requested.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub(super) struct VisitRequest {
    #[serde(default)]
    pub visit_id: i32,
}

impl Validate for VisitRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("visitId", self.visit_id, 0);
    }
}

/// Identifies the vitals record whose child record is requested.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub(super) struct VitalsRequest {
    #[serde(default)]
    pub vitals_id: i32,
}

impl Validate for VitalsRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("vitalsId", self.vitals_id, 0);
    }
}

/// Identifies the patient whose records are listed.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub(super) struct PatientRequest {
    #[serde(default)]
    pub patient_id: i32,
}

impl Validate for PatientRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("patientId", self.patient_id, 0);
    }
}

pub(super) async fn ensure_patient(conn: &mut PgConnection, patient_id: i32) -> ApiResult<()> {
    if exists(conn, "Patients", "Id", patient_id).await? {
        Ok(())
    } else {
        Err(ApiError::not_found("Patient was not found."))
    }
}

pub(super) async fn ensure_visit(conn: &mut PgConnection, visit_id: i32) -> ApiResult<()> {
    if exists(conn, "Visits", "Id", visit_id).await? {
        Ok(())
    } else {
        Err(ApiError::not_found("Visit was not found."))
    }
}

pub(super) async fn ensure_vitals(conn: &mut PgConnection, vitals_id: i32) -> ApiResult<()> {
    if exists(conn, "Vitals", "Id", vitals_id).await? { Ok(()) } else { Err(ApiError::not_found(NOT_FOUND)) }
}

/// Vitals recorded for a visit.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct VitalsResponse {
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

impl From<Vitals> for VitalsResponse {
    fn from(vitals: Vitals) -> Self {
        VitalsResponse {
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

async fn find_by_id(conn: &mut PgConnection, id: i32) -> ApiResult<Vitals> {
    sqlx::query_as(r#"SELECT * FROM "Vitals" WHERE "Id" = $1"#)
        .bind(id)
        .fetch_optional(conn)
        .await
        .or_not_found(NOT_FOUND)
}

async fn find_by_visit(conn: &mut PgConnection, visit_id: i32) -> ApiResult<Vitals> {
    sqlx::query_as(r#"SELECT * FROM "Vitals" WHERE "VisitId" = $1 LIMIT 1"#)
        .bind(visit_id)
        .fetch_optional(conn)
        .await
        .or_not_found(NOT_FOUND)
}

/// Measurements to record against a visit.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateRequest {
    #[serde(default)]
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

impl Validate for CreateRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("visitId", self.visit_id, 0);
    }
}

/// Record vitals for a visit.
async fn create(ctx: Ctx, _: Staff, Req(req): Req<CreateRequest>) -> ApiResult<Created<VitalsResponse>> {
    let mut tx = ctx.begin().await?;
    ensure_visit(&mut tx, req.visit_id).await?;
    if exists(&mut tx, "Vitals", "VisitId", req.visit_id).await? {
        return Err(ApiError::conflict(ALREADY_EXISTS));
    }

    let mut vitals = Vitals {
        id: 0,
        visit_id: req.visit_id,
        height: req.height,
        weight: req.weight,
        temperature: req.temperature,
        systolic: req.systolic,
        diastolic: req.diastolic,
        heart_rate: req.heart_rate,
        hemocue_count: req.hemocue_count,
        diabetes_mellitus: req.diabetes_mellitus,
        urine_test: free_text(req.urine_test.as_deref()),
        blood_glucose_non_fasting: req.blood_glucose_non_fasting,
        blood_glucose_fasting: req.blood_glucose_fasting,
        hba1c: req.hba1c,
        others: free_text(req.others.as_deref()),
        recorded_at: None,
    };
    let stored = tx.insert(vitals.clone()).await.map_err(|error| conflict_if_duplicate(error, ALREADY_EXISTS))?;
    tx.commit().await?;

    // The measurements are returned as they were sent; the columns keep two decimals.
    vitals.id = stored.id;
    Ok(Created::at(format!("/vitals/{}", vitals.id), vitals.into()))
}

/// Get vitals by id.
async fn get_one(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<Json<VitalsResponse>> {
    let mut conn = ctx.db().acquire().await?;
    Ok(Json(find_by_id(&mut conn, req.id).await?.into()))
}

/// Get vitals for a visit.
async fn get_by_visit(ctx: Ctx, _: Staff, Req(req): Req<VisitRequest>) -> ApiResult<Json<VitalsResponse>> {
    let mut conn = ctx.db().acquire().await?;
    Ok(Json(find_by_visit(&mut conn, req.visit_id).await?.into()))
}

/// List a patient's vitals.
async fn list_by_patient(ctx: Ctx, _: Staff, Req(req): Req<PatientRequest>) -> ApiResult<Json<Vec<VitalsResponse>>> {
    let mut conn = ctx.db().acquire().await?;
    ensure_patient(&mut conn, req.patient_id).await?;
    let rows: Vec<Vitals> = sqlx::query_as(
        r#"SELECT v.* FROM "Vitals" v JOIN "Visits" s ON s."Id" = v."VisitId"
           WHERE s."PatientId" = $1 ORDER BY s."Date" DESC, v."Id" DESC"#,
    )
    .bind(req.patient_id)
    .fetch_all(&mut *conn)
    .await?;
    Ok(Json(rows.into_iter().map(Into::into).collect()))
}

/// Partial vitals update. Omitted measurements are left unchanged, and the visit cannot be changed.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateRequest {
    #[serde(default)]
    pub id: i32,
    #[serde(default)]
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

impl Validate for ById<UpdateRequest> {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("id", self.0.id, 0);
    }
}

impl Validate for ByVisit<UpdateRequest> {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("visitId", self.0.visit_id, 0);
    }
}

fn apply(vitals: &mut Vitals, req: &UpdateRequest) {
    fn set<T: Clone>(field: &mut Option<T>, value: &Option<T>) {
        if value.is_some() {
            field.clone_from(value);
        }
    }
    set(&mut vitals.height, &req.height);
    set(&mut vitals.weight, &req.weight);
    set(&mut vitals.temperature, &req.temperature);
    set(&mut vitals.systolic, &req.systolic);
    set(&mut vitals.diastolic, &req.diastolic);
    set(&mut vitals.heart_rate, &req.heart_rate);
    set(&mut vitals.hemocue_count, &req.hemocue_count);
    set(&mut vitals.diabetes_mellitus, &req.diabetes_mellitus);
    if req.urine_test.is_some() {
        vitals.urine_test = free_text(req.urine_test.as_deref());
    }
    set(&mut vitals.blood_glucose_non_fasting, &req.blood_glucose_non_fasting);
    set(&mut vitals.blood_glucose_fasting, &req.blood_glucose_fasting);
    set(&mut vitals.hba1c, &req.hba1c);
    if req.others.is_some() {
        vitals.others = free_text(req.others.as_deref());
    }
}

/// Update vitals.
async fn update(ctx: Ctx, _: Staff, Req(ById(req)): Req<ById<UpdateRequest>>) -> ApiResult<Json<VitalsResponse>> {
    let mut tx = ctx.begin().await?;
    let before = find_by_id(&mut tx, req.id).await?;
    let mut vitals = before.clone();
    apply(&mut vitals, &req);
    let vitals = tx.update(&before, vitals).await?;
    tx.commit().await?;
    Ok(Json(vitals.into()))
}

/// Update vitals for a visit.
async fn update_by_visit(
    ctx: Ctx,
    _: Staff,
    Req(ByVisit(req)): Req<ByVisit<UpdateRequest>>,
) -> ApiResult<Json<VitalsResponse>> {
    let mut tx = ctx.begin().await?;
    let before = find_by_visit(&mut tx, req.visit_id).await?;
    let mut vitals = before.clone();
    apply(&mut vitals, &req);
    let vitals = tx.update(&before, vitals).await?;
    tx.commit().await?;
    Ok(Json(vitals.into()))
}

/// Delete vitals.
async fn delete(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<NoContent> {
    let mut tx = ctx.begin().await?;
    let vitals = find_by_id(&mut tx, req.id).await?;
    tx.delete(&vitals).await?;
    tx.commit().await?;
    Ok(NoContent)
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/vitals", post(create))
        .route("/vitals/{id}", get(get_one).patch(update).delete(delete))
        .route("/visits/{visitId}/vitals", get(get_by_visit).patch(update_by_visit))
        .route("/patients/{patientId}/vitals", get(list_by_patient))
}

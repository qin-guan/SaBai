//! Eyesight: acuity, pinhole and astigmatism readings for a visit. One record per visit;
//! the glasses prescribed from it are kept on the same record (see `glasses`).

use axum::Router;
use axum::routing::{get, post};
use serde::{Deserialize, Serialize};
use sqlx::PgConnection;

use super::vitals::{
    ById, ByVisit, IdRequest, PatientRequest, VisitRequest, conflict_if_duplicate, ensure_patient, ensure_visit,
    exists, free_text,
};
use crate::auth::Staff;
use crate::domain::Eyesight;
use crate::domain::eyesight_rules::{normalize_acuity, normalize_pinhole};
use crate::error::{ApiError, ApiResult, OrNotFound};
use crate::http::{Created, Ctx, Json, NoContent, Req, Rules, Validate};
use crate::state::AppState;

const NOT_FOUND: &str = "Eyesight was not found.";
const ALREADY_EXISTS: &str = "Eyesight already exists for this visit.";

/// Eyesight recorded for a visit.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct EyesightResponse {
    pub id: i32,
    pub visit_id: i32,
    pub left_eye_degree: Option<String>,
    pub right_eye_degree: Option<String>,
    pub left_eye_pinhole: Option<String>,
    pub right_eye_pinhole: Option<String>,
    pub left_astigmatism: Option<String>,
    pub right_astigmatism: Option<String>,
    pub comments: Option<String>,
    pub left_prescribed_glasses_degree: Option<String>,
    pub right_prescribed_glasses_degree: Option<String>,
}

impl From<Eyesight> for EyesightResponse {
    fn from(eyesight: Eyesight) -> Self {
        EyesightResponse {
            id: eyesight.id,
            visit_id: eyesight.visit_id,
            left_eye_degree: eyesight.left_eye_degree,
            right_eye_degree: eyesight.right_eye_degree,
            left_eye_pinhole: eyesight.left_eye_pinhole,
            right_eye_pinhole: eyesight.right_eye_pinhole,
            left_astigmatism: eyesight.left_astigmatism,
            right_astigmatism: eyesight.right_astigmatism,
            comments: eyesight.comments,
            left_prescribed_glasses_degree: eyesight.left_prescribed_glasses_degree,
            right_prescribed_glasses_degree: eyesight.right_prescribed_glasses_degree,
        }
    }
}

pub(super) async fn find_by_id(conn: &mut PgConnection, id: i32, not_found: &str) -> ApiResult<Eyesight> {
    sqlx::query_as(r#"SELECT * FROM "Eyesights" WHERE "Id" = $1"#)
        .bind(id)
        .fetch_optional(conn)
        .await
        .or_not_found(not_found)
}

/// The visit's eyesight record. A visit has at most one; the newest wins should that ever not hold.
pub(super) async fn find_by_visit(conn: &mut PgConnection, visit_id: i32) -> Result<Option<Eyesight>, sqlx::Error> {
    sqlx::query_as(r#"SELECT * FROM "Eyesights" WHERE "VisitId" = $1 ORDER BY "Id" DESC LIMIT 1"#)
        .bind(visit_id)
        .fetch_optional(conn)
        .await
}

/// Eyesight readings to record against a visit.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateRequest {
    #[serde(default)]
    pub visit_id: i32,
    pub left_eye_degree: Option<String>,
    pub right_eye_degree: Option<String>,
    pub left_eye_pinhole: Option<String>,
    pub right_eye_pinhole: Option<String>,
    pub left_astigmatism: Option<String>,
    pub right_astigmatism: Option<String>,
    pub comments: Option<String>,
    pub left_prescribed_glasses_degree: Option<String>,
    pub right_prescribed_glasses_degree: Option<String>,
}

impl Validate for CreateRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("visitId", self.visit_id, 0);
    }
}

/// Record eyesight for a visit.
async fn create(ctx: Ctx, _: Staff, Req(req): Req<CreateRequest>) -> ApiResult<Created<EyesightResponse>> {
    let left_eye_degree = normalize_acuity(req.left_eye_degree.as_deref())?;
    let right_eye_degree = normalize_acuity(req.right_eye_degree.as_deref())?;
    let left_eye_pinhole = normalize_pinhole(req.left_eye_pinhole.as_deref())?;
    let right_eye_pinhole = normalize_pinhole(req.right_eye_pinhole.as_deref())?;

    let mut tx = ctx.begin().await?;
    ensure_visit(&mut tx, req.visit_id).await?;
    if exists(&mut tx, "Eyesights", "VisitId", req.visit_id).await? {
        return Err(ApiError::conflict(ALREADY_EXISTS));
    }

    let eyesight = tx
        .insert(Eyesight {
            id: 0,
            visit_id: req.visit_id,
            left_eye_degree,
            right_eye_degree,
            left_eye_pinhole,
            right_eye_pinhole,
            left_astigmatism: free_text(req.left_astigmatism.as_deref()),
            right_astigmatism: free_text(req.right_astigmatism.as_deref()),
            comments: free_text(req.comments.as_deref()),
            left_prescribed_glasses_degree: free_text(req.left_prescribed_glasses_degree.as_deref()),
            right_prescribed_glasses_degree: free_text(req.right_prescribed_glasses_degree.as_deref()),
            recorded_at: None,
        })
        .await
        .map_err(|error| conflict_if_duplicate(error, ALREADY_EXISTS))?;
    tx.commit().await?;
    Ok(Created::at(format!("/eyesight/{}", eyesight.id), eyesight.into()))
}

/// Get eyesight by id.
async fn get_one(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<Json<EyesightResponse>> {
    let mut conn = ctx.db().acquire().await?;
    Ok(Json(find_by_id(&mut conn, req.id, NOT_FOUND).await?.into()))
}

/// Get eyesight for a visit.
async fn get_by_visit(ctx: Ctx, _: Staff, Req(req): Req<VisitRequest>) -> ApiResult<Json<EyesightResponse>> {
    let mut conn = ctx.db().acquire().await?;
    Ok(Json(find_by_visit(&mut conn, req.visit_id).await.or_not_found(NOT_FOUND)?.into()))
}

/// List a patient's eyesight records.
async fn list_by_patient(ctx: Ctx, _: Staff, Req(req): Req<PatientRequest>) -> ApiResult<Json<Vec<EyesightResponse>>> {
    let mut conn = ctx.db().acquire().await?;
    ensure_patient(&mut conn, req.patient_id).await?;
    let rows: Vec<Eyesight> = sqlx::query_as(
        r#"SELECT e.* FROM "Eyesights" e JOIN "Visits" s ON s."Id" = e."VisitId"
           WHERE s."PatientId" = $1 ORDER BY s."Date" DESC, e."Id" DESC"#,
    )
    .bind(req.patient_id)
    .fetch_all(&mut *conn)
    .await?;
    Ok(Json(rows.into_iter().map(Into::into).collect()))
}

/// Partial eyesight update. Omitted readings are left unchanged, and the visit cannot be changed.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateRequest {
    #[serde(default)]
    pub id: i32,
    #[serde(default)]
    pub visit_id: i32,
    pub left_eye_degree: Option<String>,
    pub right_eye_degree: Option<String>,
    pub left_eye_pinhole: Option<String>,
    pub right_eye_pinhole: Option<String>,
    pub left_astigmatism: Option<String>,
    pub right_astigmatism: Option<String>,
    pub comments: Option<String>,
    pub left_prescribed_glasses_degree: Option<String>,
    pub right_prescribed_glasses_degree: Option<String>,
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

fn apply(eyesight: &mut Eyesight, req: &UpdateRequest) -> ApiResult<()> {
    fn set(field: &mut Option<String>, value: &Option<String>) {
        if value.is_some() {
            *field = free_text(value.as_deref());
        }
    }
    if req.left_eye_degree.is_some() {
        eyesight.left_eye_degree = normalize_acuity(req.left_eye_degree.as_deref())?;
    }
    if req.right_eye_degree.is_some() {
        eyesight.right_eye_degree = normalize_acuity(req.right_eye_degree.as_deref())?;
    }
    if req.left_eye_pinhole.is_some() {
        eyesight.left_eye_pinhole = normalize_pinhole(req.left_eye_pinhole.as_deref())?;
    }
    if req.right_eye_pinhole.is_some() {
        eyesight.right_eye_pinhole = normalize_pinhole(req.right_eye_pinhole.as_deref())?;
    }
    set(&mut eyesight.left_astigmatism, &req.left_astigmatism);
    set(&mut eyesight.right_astigmatism, &req.right_astigmatism);
    set(&mut eyesight.comments, &req.comments);
    set(&mut eyesight.left_prescribed_glasses_degree, &req.left_prescribed_glasses_degree);
    set(&mut eyesight.right_prescribed_glasses_degree, &req.right_prescribed_glasses_degree);
    Ok(())
}

/// Update eyesight.
async fn update(ctx: Ctx, _: Staff, Req(ById(req)): Req<ById<UpdateRequest>>) -> ApiResult<Json<EyesightResponse>> {
    let mut tx = ctx.begin().await?;
    let before = find_by_id(&mut tx, req.id, NOT_FOUND).await?;
    let mut eyesight = before.clone();
    apply(&mut eyesight, &req)?;
    let eyesight = tx.update(&before, eyesight).await?;
    tx.commit().await?;
    Ok(Json(eyesight.into()))
}

/// Update eyesight for a visit.
async fn update_by_visit(
    ctx: Ctx,
    _: Staff,
    Req(ByVisit(req)): Req<ByVisit<UpdateRequest>>,
) -> ApiResult<Json<EyesightResponse>> {
    let mut tx = ctx.begin().await?;
    let before = find_by_visit(&mut tx, req.visit_id).await.or_not_found(NOT_FOUND)?;
    let mut eyesight = before.clone();
    apply(&mut eyesight, &req)?;
    let eyesight = tx.update(&before, eyesight).await?;
    tx.commit().await?;
    Ok(Json(eyesight.into()))
}

/// Delete eyesight.
async fn delete(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<NoContent> {
    let mut tx = ctx.begin().await?;
    let eyesight = find_by_id(&mut tx, req.id, NOT_FOUND).await?;
    tx.delete(&eyesight).await?;
    tx.commit().await?;
    Ok(NoContent)
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/eyesight", post(create))
        .route("/eyesight/{id}", get(get_one).patch(update).delete(delete))
        .route("/visits/{visitId}/eyesight", get(get_by_visit).patch(update_by_visit))
        .route("/patients/{patientId}/eyesight", get(list_by_patient))
}

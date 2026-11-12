//! Diagnoses: the conditions a doctor recorded on a consult.

use axum::Router;
use axum::routing::get;
use serde::{Deserialize, Serialize};
use sqlx::PgConnection;

use crate::auth::Staff;
use crate::domain::{Diagnosis, DiagnosisCategory};
use crate::error::{ApiError, ApiResult, OrNotFound};
use crate::http::{Created, Ctx, Json, NoContent, Req, Rules, Validate};
use crate::state::AppState;

/// Diagnosis row.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct DiagnosisResponse {
    pub id: i32,
    pub consult_id: i32,
    pub details: String,
    pub category: Option<DiagnosisCategory>,
}

impl From<Diagnosis> for DiagnosisResponse {
    fn from(diagnosis: Diagnosis) -> Self {
        DiagnosisResponse {
            id: diagnosis.id,
            consult_id: diagnosis.consult_id,
            details: diagnosis.details,
            category: diagnosis.category,
        }
    }
}

/// Identifies a diagnosis.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct IdRequest {
    #[serde(default)]
    pub id: i32,
}

impl Validate for IdRequest {}

async fn find(conn: &mut PgConnection, id: i32) -> ApiResult<Diagnosis> {
    sqlx::query_as(r#"SELECT * FROM "Diagnoses" WHERE "Id" = $1"#)
        .bind(id)
        .fetch_optional(conn)
        .await
        .or_not_found("Diagnosis was not found.")
}

/// Get a diagnosis.
async fn get_one(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<Json<DiagnosisResponse>> {
    let mut conn = ctx.db().acquire().await?;
    Ok(Json(find(&mut conn, req.id).await?.into()))
}

/// Optional consult filter for the diagnosis list.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ListRequest {
    pub consult_id: Option<i32>,
}

impl Validate for ListRequest {}

/// List diagnoses, optionally for one consult.
async fn list(ctx: Ctx, _: Staff, Req(req): Req<ListRequest>) -> ApiResult<Json<Vec<DiagnosisResponse>>> {
    let consult_id = req.consult_id.filter(|&id| id > 0);
    let diagnoses: Vec<Diagnosis> =
        sqlx::query_as(r#"SELECT * FROM "Diagnoses" WHERE ($1::int IS NULL OR "ConsultId" = $1) ORDER BY "Id""#)
            .bind(consult_id)
            .fetch_all(ctx.db())
            .await?;
    Ok(Json(diagnoses.into_iter().map(Into::into).collect()))
}

/// Diagnosis to add to an existing consult.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateRequest {
    #[serde(default)]
    pub consult_id: i32,
    #[serde(default)]
    pub details: String,
    pub category: Option<DiagnosisCategory>,
}

impl Validate for CreateRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("consultId", self.consult_id, 0);
        v.not_empty("details", &self.details);
    }
}

/// Add a diagnosis to a consult.
async fn create(ctx: Ctx, _: Staff, Req(req): Req<CreateRequest>) -> ApiResult<Created<DiagnosisResponse>> {
    let mut tx = ctx.begin().await?;
    let consult_exists: bool = sqlx::query_scalar(r#"SELECT EXISTS (SELECT 1 FROM "Consults" WHERE "Id" = $1)"#)
        .bind(req.consult_id)
        .fetch_one(&mut *tx)
        .await?;
    if !consult_exists {
        return Err(ApiError::not_found("Consult was not found."));
    }
    let diagnosis = tx
        .insert(Diagnosis {
            id: 0,
            details: req.details.trim().to_owned(),
            category: req.category,
            consult_id: req.consult_id,
        })
        .await?;
    tx.commit().await?;
    Ok(Created::at(format!("/diagnoses/{}", diagnosis.id), diagnosis.into()))
}

/// Partial diagnosis update. Omitted fields are left unchanged.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateRequest {
    #[serde(default)]
    pub id: i32,
    pub details: Option<String>,
    pub category: Option<DiagnosisCategory>,
}

impl Validate for UpdateRequest {
    fn validate(&self, v: &mut Rules) {
        if self.details.is_some() {
            v.not_empty("details", &self.details);
        }
    }
}

/// Update a diagnosis.
async fn update(ctx: Ctx, _: Staff, Req(req): Req<UpdateRequest>) -> ApiResult<Json<DiagnosisResponse>> {
    let mut tx = ctx.begin().await?;
    let before = find(&mut tx, req.id).await?;
    let mut diagnosis = before.clone();
    if let Some(details) = &req.details {
        diagnosis.details = details.trim().to_owned();
    }
    if req.category.is_some() {
        diagnosis.category = req.category;
    }
    let diagnosis = tx.update(&before, diagnosis).await?;
    tx.commit().await?;
    Ok(Json(diagnosis.into()))
}

/// Delete a diagnosis.
async fn delete(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<NoContent> {
    let mut tx = ctx.begin().await?;
    let diagnosis = find(&mut tx, req.id).await?;
    tx.delete(&diagnosis).await?;
    tx.commit().await?;
    Ok(NoContent)
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/diagnoses", get(list).post(create))
        .route("/diagnoses/{id}", get(get_one).patch(update).delete(delete))
}

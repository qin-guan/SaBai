//! Child vitals: the paediatric examination recorded against a vitals record.

use axum::Router;
use axum::routing::{get, post};
use serde::{Deserialize, Serialize};
use sqlx::PgConnection;

use super::vitals::{
    ById, ByVitals, IdRequest, VitalsRequest, conflict_if_duplicate, ensure_vitals, exists, free_text,
};
use crate::auth::Staff;
use crate::domain::ChildVitals;
use crate::error::{ApiError, ApiResult, OrNotFound};
use crate::http::{Created, Ctx, Json, NoContent, Req, Rules, Validate};
use crate::state::AppState;

const NOT_FOUND: &str = "Child vitals were not found.";
const ALREADY_EXISTS: &str = "Child vitals already exist for this vitals record.";

/// Child vitals recorded against a vitals record.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ChildVitalsResponse {
    pub id: i32,
    pub vitals_id: i32,
    pub gross_motor: Option<String>,
    pub red_reflex: Option<String>,
    pub scoliosis: Option<String>,
    pub pallor: Option<bool>,
    pub oral_cavity: Option<String>,
    pub heart: Option<String>,
    pub abdomen: Option<String>,
    pub lungs: Option<String>,
    pub hernial_orifices: Option<String>,
}

impl From<ChildVitals> for ChildVitalsResponse {
    fn from(child: ChildVitals) -> Self {
        ChildVitalsResponse {
            id: child.id,
            vitals_id: child.vitals_id,
            gross_motor: child.gross_motor,
            red_reflex: child.red_reflex,
            scoliosis: child.scoliosis,
            pallor: child.pallor,
            oral_cavity: child.oral_cavity,
            heart: child.heart,
            abdomen: child.abdomen,
            lungs: child.lungs,
            hernial_orifices: child.hernial_orifices,
        }
    }
}

async fn find_by_id(conn: &mut PgConnection, id: i32) -> ApiResult<ChildVitals> {
    sqlx::query_as(r#"SELECT * FROM "ChildVitals" WHERE "Id" = $1"#)
        .bind(id)
        .fetch_optional(conn)
        .await
        .or_not_found(NOT_FOUND)
}

async fn find_by_vitals(conn: &mut PgConnection, vitals_id: i32) -> ApiResult<ChildVitals> {
    sqlx::query_as(r#"SELECT * FROM "ChildVitals" WHERE "VitalsId" = $1 LIMIT 1"#)
        .bind(vitals_id)
        .fetch_optional(conn)
        .await
        .or_not_found(NOT_FOUND)
}

/// Child vitals to record against a vitals record.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateRequest {
    #[serde(default)]
    pub vitals_id: i32,
    pub gross_motor: Option<String>,
    pub red_reflex: Option<String>,
    pub scoliosis: Option<String>,
    pub pallor: Option<bool>,
    pub oral_cavity: Option<String>,
    pub heart: Option<String>,
    pub abdomen: Option<String>,
    pub lungs: Option<String>,
    pub hernial_orifices: Option<String>,
}

impl Validate for CreateRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("vitalsId", self.vitals_id, 0);
    }
}

/// Record child vitals.
async fn create(ctx: Ctx, _: Staff, Req(req): Req<CreateRequest>) -> ApiResult<Created<ChildVitalsResponse>> {
    let mut tx = ctx.begin().await?;
    ensure_vitals(&mut tx, req.vitals_id).await?;
    if exists(&mut tx, "ChildVitals", "VitalsId", req.vitals_id).await? {
        return Err(ApiError::conflict(ALREADY_EXISTS));
    }

    let child = tx
        .insert(ChildVitals {
            id: 0,
            vitals_id: req.vitals_id,
            gross_motor: free_text(req.gross_motor.as_deref()),
            red_reflex: free_text(req.red_reflex.as_deref()),
            scoliosis: free_text(req.scoliosis.as_deref()),
            pallor: req.pallor,
            oral_cavity: free_text(req.oral_cavity.as_deref()),
            heart: free_text(req.heart.as_deref()),
            abdomen: free_text(req.abdomen.as_deref()),
            lungs: free_text(req.lungs.as_deref()),
            hernial_orifices: free_text(req.hernial_orifices.as_deref()),
        })
        .await
        .map_err(|error| conflict_if_duplicate(error, ALREADY_EXISTS))?;
    tx.commit().await?;
    Ok(Created::at(format!("/child-vitals/{}", child.id), child.into()))
}

/// Get child vitals by id.
async fn get_one(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<Json<ChildVitalsResponse>> {
    let mut conn = ctx.db().acquire().await?;
    Ok(Json(find_by_id(&mut conn, req.id).await?.into()))
}

/// Get child vitals for a vitals record.
async fn get_by_vitals(ctx: Ctx, _: Staff, Req(req): Req<VitalsRequest>) -> ApiResult<Json<ChildVitalsResponse>> {
    let mut conn = ctx.db().acquire().await?;
    Ok(Json(find_by_vitals(&mut conn, req.vitals_id).await?.into()))
}

/// Partial child vitals update. Omitted fields are left unchanged, and the vitals record cannot be changed.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateRequest {
    #[serde(default)]
    pub id: i32,
    #[serde(default)]
    pub vitals_id: i32,
    pub gross_motor: Option<String>,
    pub red_reflex: Option<String>,
    pub scoliosis: Option<String>,
    pub pallor: Option<bool>,
    pub oral_cavity: Option<String>,
    pub heart: Option<String>,
    pub abdomen: Option<String>,
    pub lungs: Option<String>,
    pub hernial_orifices: Option<String>,
}

impl Validate for ById<UpdateRequest> {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("id", self.0.id, 0);
    }
}

impl Validate for ByVitals<UpdateRequest> {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("vitalsId", self.0.vitals_id, 0);
    }
}

fn apply(child: &mut ChildVitals, req: &UpdateRequest) {
    fn set(field: &mut Option<String>, value: &Option<String>) {
        if value.is_some() {
            *field = free_text(value.as_deref());
        }
    }
    set(&mut child.gross_motor, &req.gross_motor);
    set(&mut child.red_reflex, &req.red_reflex);
    set(&mut child.scoliosis, &req.scoliosis);
    if req.pallor.is_some() {
        child.pallor = req.pallor;
    }
    set(&mut child.oral_cavity, &req.oral_cavity);
    set(&mut child.heart, &req.heart);
    set(&mut child.abdomen, &req.abdomen);
    set(&mut child.lungs, &req.lungs);
    set(&mut child.hernial_orifices, &req.hernial_orifices);
}

/// Update child vitals.
async fn update(ctx: Ctx, _: Staff, Req(ById(req)): Req<ById<UpdateRequest>>) -> ApiResult<Json<ChildVitalsResponse>> {
    let mut tx = ctx.begin().await?;
    let before = find_by_id(&mut tx, req.id).await?;
    let mut child = before.clone();
    apply(&mut child, &req);
    let child = tx.update(&before, child).await?;
    tx.commit().await?;
    Ok(Json(child.into()))
}

/// Update child vitals for a vitals record.
async fn update_by_vitals(
    ctx: Ctx,
    _: Staff,
    Req(ByVitals(req)): Req<ByVitals<UpdateRequest>>,
) -> ApiResult<Json<ChildVitalsResponse>> {
    let mut tx = ctx.begin().await?;
    let before = find_by_vitals(&mut tx, req.vitals_id).await?;
    let mut child = before.clone();
    apply(&mut child, &req);
    let child = tx.update(&before, child).await?;
    tx.commit().await?;
    Ok(Json(child.into()))
}

/// Delete child vitals.
async fn delete(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<NoContent> {
    let mut tx = ctx.begin().await?;
    let child = find_by_id(&mut tx, req.id).await?;
    tx.delete(&child).await?;
    tx.commit().await?;
    Ok(NoContent)
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/child-vitals", post(create))
        .route("/child-vitals/{id}", get(get_one).patch(update).delete(delete))
        .route("/vitals/{vitalsId}/child-vitals", get(get_by_vitals).patch(update_by_vitals))
}

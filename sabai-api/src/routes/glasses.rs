//! Prescribed glasses: a view of the prescription fields on a visit's eyesight record.

use axum::Router;
use axum::routing::get;
use serde::{Deserialize, Serialize};

use super::eyesight::{find_by_id, find_by_visit};
use super::vitals::{ById, ByVisit, IdRequest, VisitRequest, ensure_visit, free_text, is_duplicate};
use crate::auth::Staff;
use crate::db::Tx;
use crate::domain::Eyesight;
use crate::error::{ApiResult, OrNotFound};
use crate::http::{Created, Ctx, Json, Req, Rules, Validate};
use crate::state::AppState;

const NOT_FOUND: &str = "Glasses were not found.";

/// Prescribed glasses degrees recorded against a visit.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct GlassesResponse {
    pub id: i32,
    pub visit_id: i32,
    pub left_glasses_degree: Option<String>,
    pub right_glasses_degree: Option<String>,
    pub notes: Option<String>,
}

impl From<Eyesight> for GlassesResponse {
    fn from(eyesight: Eyesight) -> Self {
        GlassesResponse {
            id: eyesight.id,
            visit_id: eyesight.visit_id,
            left_glasses_degree: eyesight.left_prescribed_glasses_degree,
            right_glasses_degree: eyesight.right_prescribed_glasses_degree,
            notes: eyesight.comments,
        }
    }
}

/// Prescribed glasses degrees to record against a visit, or a partial update of them
/// (by the eyesight record id): omitted fields are left unchanged and empty strings
/// clear a field. The visit cannot be changed.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Prescription {
    #[serde(default)]
    pub id: i32,
    #[serde(default)]
    pub visit_id: i32,
    pub left_glasses_degree: Option<String>,
    pub right_glasses_degree: Option<String>,
    pub notes: Option<String>,
}

impl Prescription {
    fn apply_provided(&self, eyesight: &mut Eyesight) {
        if self.left_glasses_degree.is_some() {
            eyesight.left_prescribed_glasses_degree = free_text(self.left_glasses_degree.as_deref());
        }
        if self.right_glasses_degree.is_some() {
            eyesight.right_prescribed_glasses_degree = free_text(self.right_glasses_degree.as_deref());
        }
        if self.notes.is_some() {
            eyesight.comments = free_text(self.notes.as_deref());
        }
    }
}

impl Validate for ByVisit<Prescription> {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("visitId", self.0.visit_id, 0);
    }
}

impl Validate for ById<Prescription> {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("id", self.0.id, 0);
    }
}

async fn update_existing(mut tx: Tx, before: Eyesight, req: &Prescription) -> ApiResult<Eyesight> {
    let mut eyesight = before.clone();
    req.apply_provided(&mut eyesight);
    let eyesight = tx.update(&before, eyesight).await?;
    tx.commit().await?;
    Ok(eyesight)
}

/// Record prescribed glasses for a visit.
async fn create(
    ctx: Ctx,
    _: Staff,
    Req(ByVisit(req)): Req<ByVisit<Prescription>>,
) -> ApiResult<Created<GlassesResponse>> {
    let mut tx = ctx.begin().await?;
    ensure_visit(&mut tx, req.visit_id).await?;

    let eyesight = match find_by_visit(&mut tx, req.visit_id).await? {
        Some(existing) => update_existing(tx, existing, &req).await?,
        None => {
            let inserted = tx
                .insert(Eyesight {
                    id: 0,
                    visit_id: req.visit_id,
                    left_eye_degree: None,
                    right_eye_degree: None,
                    left_eye_pinhole: None,
                    right_eye_pinhole: None,
                    left_astigmatism: None,
                    right_astigmatism: None,
                    comments: free_text(req.notes.as_deref()),
                    left_prescribed_glasses_degree: free_text(req.left_glasses_degree.as_deref()),
                    right_prescribed_glasses_degree: free_text(req.right_glasses_degree.as_deref()),
                    recorded_at: None,
                })
                .await;
            match inserted {
                Ok(eyesight) => {
                    tx.commit().await?;
                    eyesight
                }
                // The eyesight station saved its record in the meantime: add the prescription to that one.
                Err(error) if is_duplicate(&error) => {
                    tx.rollback().await?;
                    let mut tx = ctx.begin().await?;
                    let existing = find_by_visit(&mut tx, req.visit_id).await.or_not_found(NOT_FOUND)?;
                    update_existing(tx, existing, &req).await?
                }
                Err(error) => return Err(error.into()),
            }
        }
    };
    Ok(Created::at(format!("/glasses/{}", eyesight.id), eyesight.into()))
}

/// Get prescribed glasses by id.
async fn get_one(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<Json<GlassesResponse>> {
    let mut conn = ctx.db().acquire().await?;
    Ok(Json(find_by_id(&mut conn, req.id, NOT_FOUND).await?.into()))
}

/// Get prescribed glasses for a visit.
async fn get_by_visit(ctx: Ctx, _: Staff, Req(req): Req<VisitRequest>) -> ApiResult<Json<GlassesResponse>> {
    let mut conn = ctx.db().acquire().await?;
    Ok(Json(find_by_visit(&mut conn, req.visit_id).await.or_not_found(NOT_FOUND)?.into()))
}

/// Update prescribed glasses.
async fn update(ctx: Ctx, _: Staff, Req(ById(req)): Req<ById<Prescription>>) -> ApiResult<Json<GlassesResponse>> {
    let mut tx = ctx.begin().await?;
    let before = find_by_id(&mut tx, req.id, NOT_FOUND).await?;
    Ok(Json(update_existing(tx, before, &req).await?.into()))
}

pub fn router() -> Router<AppState> {
    Router::new().route("/glasses", get(get_by_visit).post(create)).route("/glasses/{id}", get(get_one).patch(update))
}

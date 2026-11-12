//! Puberty assessments recorded against a vitals record.

use axum::Router;
use axum::routing::{get, post};
use serde::{Deserialize, Serialize};
use sqlx::PgConnection;

use super::vitals::{
    ById, ByVitals, IdRequest, VitalsRequest, conflict_if_duplicate, ensure_vitals, exists, free_text,
};
use crate::auth::Staff;
use crate::domain::PubertyAssessment;
use crate::error::{ApiError, ApiResult, OrNotFound};
use crate::http::{Created, Ctx, Json, NoContent, Req, Rules, Validate};
use crate::state::AppState;

const NOT_FOUND: &str = "Puberty assessment was not found.";
const ALREADY_EXISTS: &str = "A puberty assessment already exists for this vitals record.";

/// Puberty assessment recorded against vitals.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PubertyResponse {
    pub id: i32,
    pub vitals_id: i32,
    pub pubarche: Option<bool>,
    pub pubarche_age: Option<i32>,
    pub thelarche: Option<bool>,
    pub thelarche_age: Option<i32>,
    pub menarche: Option<bool>,
    pub menarche_age: Option<i32>,
    pub voice_change: Option<bool>,
    pub voice_change_age: Option<i32>,
    pub testicular_growth: Option<bool>,
    pub testicular_growth_age: Option<i32>,
    pub additional_notes: Option<String>,
}

impl From<PubertyAssessment> for PubertyResponse {
    fn from(puberty: PubertyAssessment) -> Self {
        PubertyResponse {
            id: puberty.id,
            vitals_id: puberty.vitals_id,
            pubarche: puberty.pubarche,
            pubarche_age: puberty.pubarche_age,
            thelarche: puberty.thelarche,
            thelarche_age: puberty.thelarche_age,
            menarche: puberty.menarche,
            menarche_age: puberty.menarche_age,
            voice_change: puberty.voice_change,
            voice_change_age: puberty.voice_change_age,
            testicular_growth: puberty.testicular_growth,
            testicular_growth_age: puberty.testicular_growth_age,
            additional_notes: puberty.additional_notes,
        }
    }
}

async fn find_by_id(conn: &mut PgConnection, id: i32) -> ApiResult<PubertyAssessment> {
    sqlx::query_as(r#"SELECT * FROM "PubertyAssessments" WHERE "Id" = $1"#)
        .bind(id)
        .fetch_optional(conn)
        .await
        .or_not_found(NOT_FOUND)
}

async fn find_by_vitals(conn: &mut PgConnection, vitals_id: i32) -> ApiResult<PubertyAssessment> {
    sqlx::query_as(r#"SELECT * FROM "PubertyAssessments" WHERE "VitalsId" = $1 LIMIT 1"#)
        .bind(vitals_id)
        .fetch_optional(conn)
        .await
        .or_not_found(NOT_FOUND)
}

/// The assessment's fields as a request carries them. Creating records them all;
/// updating leaves the omitted ones unchanged.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Fields {
    #[serde(default)]
    pub id: i32,
    #[serde(default)]
    pub vitals_id: i32,
    pub pubarche: Option<bool>,
    pub pubarche_age: Option<i32>,
    pub thelarche: Option<bool>,
    pub thelarche_age: Option<i32>,
    pub menarche: Option<bool>,
    pub menarche_age: Option<i32>,
    pub voice_change: Option<bool>,
    pub voice_change_age: Option<i32>,
    pub testicular_growth: Option<bool>,
    pub testicular_growth_age: Option<i32>,
    pub additional_notes: Option<String>,
}

impl Fields {
    /// An age makes no sense for a milestone that is recorded as not reached.
    fn ensure_ages(&self) -> ApiResult<()> {
        let milestones = [
            (self.pubarche, self.pubarche_age, "pubarche", "pubarcheAge"),
            (self.thelarche, self.thelarche_age, "thelarche", "thelarcheAge"),
            (self.menarche, self.menarche_age, "menarche", "menarcheAge"),
            (self.voice_change, self.voice_change_age, "voiceChange", "voiceChangeAge"),
            (self.testicular_growth, self.testicular_growth_age, "testicularGrowth", "testicularGrowthAge"),
        ];
        for (flag, age, flag_name, age_name) in milestones {
            if flag == Some(false) && age.is_some() {
                return Err(ApiError::bad_request(format!("{age_name} must not be set when {flag_name} is false")));
            }
        }
        Ok(())
    }
}

/// Puberty assessment to record against vitals.
#[derive(Debug, Deserialize)]
#[serde(transparent)]
pub struct CreateRequest(pub Fields);

impl Validate for CreateRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("vitalsId", self.0.vitals_id, 0);
    }
}

/// Record a puberty assessment.
async fn create(
    ctx: Ctx,
    _: Staff,
    Req(CreateRequest(req)): Req<CreateRequest>,
) -> ApiResult<Created<PubertyResponse>> {
    req.ensure_ages()?;
    let mut tx = ctx.begin().await?;
    ensure_vitals(&mut tx, req.vitals_id).await?;
    if exists(&mut tx, "PubertyAssessments", "VitalsId", req.vitals_id).await? {
        return Err(ApiError::conflict(ALREADY_EXISTS));
    }

    let puberty = tx
        .insert(PubertyAssessment {
            id: 0,
            vitals_id: req.vitals_id,
            pubarche: req.pubarche,
            pubarche_age: req.pubarche_age,
            thelarche: req.thelarche,
            thelarche_age: req.thelarche_age,
            menarche: req.menarche,
            menarche_age: req.menarche_age,
            voice_change: req.voice_change,
            voice_change_age: req.voice_change_age,
            testicular_growth: req.testicular_growth,
            testicular_growth_age: req.testicular_growth_age,
            additional_notes: free_text(req.additional_notes.as_deref()),
        })
        .await
        .map_err(|error| conflict_if_duplicate(error, ALREADY_EXISTS))?;
    tx.commit().await?;
    Ok(Created::at(format!("/puberty/{}", puberty.id), puberty.into()))
}

/// Get a puberty assessment by id.
async fn get_one(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<Json<PubertyResponse>> {
    let mut conn = ctx.db().acquire().await?;
    Ok(Json(find_by_id(&mut conn, req.id).await?.into()))
}

/// Get the puberty assessment for vitals.
async fn get_by_vitals(ctx: Ctx, _: Staff, Req(req): Req<VitalsRequest>) -> ApiResult<Json<PubertyResponse>> {
    let mut conn = ctx.db().acquire().await?;
    Ok(Json(find_by_vitals(&mut conn, req.vitals_id).await?.into()))
}

/// Partial puberty update. Omitted fields are left unchanged, and the vitals record cannot be changed.
pub type UpdateRequest = Fields;

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

fn apply(puberty: &mut PubertyAssessment, req: &UpdateRequest) -> ApiResult<()> {
    fn set<T: Copy>(field: &mut Option<T>, value: Option<T>) {
        if value.is_some() {
            *field = value;
        }
    }
    req.ensure_ages()?;
    set(&mut puberty.pubarche, req.pubarche);
    set(&mut puberty.pubarche_age, req.pubarche_age);
    set(&mut puberty.thelarche, req.thelarche);
    set(&mut puberty.thelarche_age, req.thelarche_age);
    set(&mut puberty.menarche, req.menarche);
    set(&mut puberty.menarche_age, req.menarche_age);
    set(&mut puberty.voice_change, req.voice_change);
    set(&mut puberty.voice_change_age, req.voice_change_age);
    set(&mut puberty.testicular_growth, req.testicular_growth);
    set(&mut puberty.testicular_growth_age, req.testicular_growth_age);
    if req.additional_notes.is_some() {
        puberty.additional_notes = free_text(req.additional_notes.as_deref());
    }
    Ok(())
}

/// Update a puberty assessment.
async fn update(ctx: Ctx, _: Staff, Req(ById(req)): Req<ById<UpdateRequest>>) -> ApiResult<Json<PubertyResponse>> {
    let mut tx = ctx.begin().await?;
    let before = find_by_id(&mut tx, req.id).await?;
    let mut puberty = before.clone();
    apply(&mut puberty, &req)?;
    let puberty = tx.update(&before, puberty).await?;
    tx.commit().await?;
    Ok(Json(puberty.into()))
}

/// Update the puberty assessment for vitals.
async fn update_by_vitals(
    ctx: Ctx,
    _: Staff,
    Req(ByVitals(req)): Req<ByVitals<UpdateRequest>>,
) -> ApiResult<Json<PubertyResponse>> {
    let mut tx = ctx.begin().await?;
    let before = find_by_vitals(&mut tx, req.vitals_id).await?;
    let mut puberty = before.clone();
    apply(&mut puberty, &req)?;
    let puberty = tx.update(&before, puberty).await?;
    tx.commit().await?;
    Ok(Json(puberty.into()))
}

/// Delete a puberty assessment.
async fn delete(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<NoContent> {
    let mut tx = ctx.begin().await?;
    let puberty = find_by_id(&mut tx, req.id).await?;
    tx.delete(&puberty).await?;
    tx.commit().await?;
    Ok(NoContent)
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/puberty", post(create))
        .route("/puberty/{id}", get(get_one).patch(update).delete(delete))
        .route("/vitals/{vitalsId}/puberty", get(get_by_vitals).patch(update_by_vitals))
}

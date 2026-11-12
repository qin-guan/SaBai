//! Referrals: patients a consult sent on to another service, and how that went.

use axum::Router;
use axum::routing::{get, patch};
use serde::{Deserialize, Serialize};
use sqlx::PgConnection;
use uuid::Uuid;

use crate::auth::Staff;
use crate::domain::{Referral, ReferralState};
use crate::error::{ApiError, ApiResult, OrNotFound};
use crate::http::{Created, Ctx, Json, Req, Rules, Validate};
use crate::state::AppState;
use crate::time::Ts;

const NOT_FOUND: &str = "Referral was not found.";

/// A referral with its consult, doctor, patient and the village of the visit it was made at.
#[derive(sqlx::FromRow)]
struct ReferralRow {
    #[sqlx(flatten)]
    referral: Referral,
    #[sqlx(rename = "ConsultDate")]
    consult_date: Ts,
    #[sqlx(rename = "DoctorId")]
    doctor_id: Uuid,
    #[sqlx(rename = "DoctorNickname")]
    doctor_nickname: String,
    #[sqlx(rename = "PatientId")]
    patient_id: i32,
    #[sqlx(rename = "PatientName")]
    patient_name: String,
    #[sqlx(rename = "HasPhoto")]
    has_photo: bool,
    #[sqlx(rename = "VillageCodeId")]
    village_code_id: i32,
    #[sqlx(rename = "VillageCode")]
    village_code: String,
    #[sqlx(rename = "VillageColorHex")]
    village_color_hex: String,
}

const SELECT_REFERRAL: &str = r#"
    SELECT r.*, c."Date" AS "ConsultDate", c."DoctorId", d."Nickname" AS "DoctorNickname",
           v."PatientId", p."Name" AS "PatientName", p."PatientImagePublicId" IS NOT NULL AS "HasPhoto",
           v."VillageCodeId", vc."Code" AS "VillageCode", vc."ColorHex" AS "VillageColorHex"
    FROM "Referrals" r
    JOIN "Consults" c ON c."Id" = r."ConsultId"
    JOIN "StaffUsers" d ON d."Id" = c."DoctorId"
    JOIN "Visits" v ON v."Id" = c."VisitId"
    JOIN "Patients" p ON p."Id" = v."PatientId"
    JOIN "VillageCodes" vc ON vc."Id" = v."VillageCodeId""#;

async fn find(conn: &mut PgConnection, id: i32) -> ApiResult<ReferralRow> {
    sqlx::query_as(&format!(r#"{SELECT_REFERRAL} WHERE r."Id" = $1"#))
        .bind(id)
        .fetch_optional(conn)
        .await
        .or_not_found(NOT_FOUND)
}

async fn find_referral(conn: &mut PgConnection, id: i32) -> ApiResult<Referral> {
    sqlx::query_as(r#"SELECT * FROM "Referrals" WHERE "Id" = $1"#)
        .bind(id)
        .fetch_optional(conn)
        .await
        .or_not_found(NOT_FOUND)
}

/// Identifies a referral.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct IdRequest {
    #[serde(default)]
    pub id: i32,
}

impl Validate for IdRequest {}

/// Referral with consult, doctor nickname, patient, and the village of the visit it was made at.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ReferralResponse {
    pub id: i32,
    pub referred_for: String,
    pub referral_notes: Option<String>,
    pub referral_state: ReferralState,
    pub referral_outcome: Option<String>,
    pub consult_id: i32,
    pub created_at: Ts,
    pub consult_date: Ts,
    pub doctor_id: Uuid,
    pub patient_id: i32,
    pub patient_name: String,
    pub has_photo: bool,
    pub village_code_id: i32,
    pub village_code: String,
    pub village_color_hex: String,
    pub doctor_nickname: String,
}

/// Get a referral.
async fn get_one(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<Json<ReferralResponse>> {
    let mut conn = ctx.db().acquire().await?;
    let row = find(&mut conn, req.id).await?;
    Ok(Json(ReferralResponse {
        id: row.referral.id,
        referred_for: row.referral.referred_for,
        referral_notes: row.referral.referral_notes,
        referral_state: row.referral.referral_state,
        referral_outcome: row.referral.referral_outcome,
        consult_id: row.referral.consult_id,
        created_at: row.referral.created_at,
        consult_date: row.consult_date,
        doctor_id: row.doctor_id,
        patient_id: row.patient_id,
        patient_name: row.patient_name,
        has_photo: row.has_photo,
        village_code_id: row.village_code_id,
        village_code: row.village_code,
        village_color_hex: row.village_color_hex,
        doctor_nickname: row.doctor_nickname,
    }))
}

/// Referral with the consult date, doctor, patient, and the village of the visit it was made at.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ListItem {
    pub id: i32,
    pub referred_for: String,
    pub referral_notes: Option<String>,
    pub referral_state: ReferralState,
    pub referral_outcome: Option<String>,
    pub consult_id: i32,
    pub created_at: Ts,
    pub consult_date: Ts,
    pub doctor_id: Uuid,
    pub patient_id: i32,
    pub patient_name: String,
    pub has_photo: bool,
    pub village_code_id: i32,
    pub village_code: String,
    pub village_color_hex: String,
}

/// List referrals, newest first.
async fn list(ctx: Ctx, _: Staff) -> ApiResult<Json<Vec<ListItem>>> {
    let rows: Vec<ReferralRow> =
        sqlx::query_as(&format!(r#"{SELECT_REFERRAL} ORDER BY r."CreatedAt" DESC, r."Id" DESC"#))
            .fetch_all(ctx.db())
            .await?;
    let referrals = rows
        .into_iter()
        .map(|row| ListItem {
            id: row.referral.id,
            referred_for: row.referral.referred_for,
            referral_notes: row.referral.referral_notes,
            referral_state: row.referral.referral_state,
            referral_outcome: row.referral.referral_outcome,
            consult_id: row.referral.consult_id,
            created_at: row.referral.created_at,
            consult_date: row.consult_date,
            doctor_id: row.doctor_id,
            patient_id: row.patient_id,
            patient_name: row.patient_name,
            has_photo: row.has_photo,
            village_code_id: row.village_code_id,
            village_code: row.village_code,
            village_color_hex: row.village_color_hex,
        })
        .collect();
    Ok(Json(referrals))
}

/// Consult whose referrals should be listed.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ListByConsultRequest {
    #[serde(default)]
    pub consult_id: i32,
}

impl Validate for ListByConsultRequest {}

/// Referral row without joined patient or doctor fields.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ConsultReferral {
    pub id: i32,
    pub referred_for: String,
    pub referral_notes: Option<String>,
    pub referral_state: ReferralState,
    pub referral_outcome: Option<String>,
    pub consult_id: i32,
    pub created_at: Ts,
}

async fn consult_exists(conn: &mut PgConnection, id: i32) -> ApiResult<()> {
    let exists: bool = sqlx::query_scalar(r#"SELECT EXISTS (SELECT 1 FROM "Consults" WHERE "Id" = $1)"#)
        .bind(id)
        .fetch_one(conn)
        .await?;
    if exists { Ok(()) } else { Err(ApiError::not_found("Consult was not found.")) }
}

/// List referrals for a consult, newest first.
async fn list_by_consult(
    ctx: Ctx,
    _: Staff,
    Req(req): Req<ListByConsultRequest>,
) -> ApiResult<Json<Vec<ConsultReferral>>> {
    let mut conn = ctx.db().acquire().await?;
    consult_exists(&mut conn, req.consult_id).await?;
    let referrals: Vec<Referral> =
        sqlx::query_as(r#"SELECT * FROM "Referrals" WHERE "ConsultId" = $1 ORDER BY "CreatedAt" DESC, "Id" DESC"#)
            .bind(req.consult_id)
            .fetch_all(&mut *conn)
            .await?;
    let referrals = referrals
        .into_iter()
        .map(|referral| ConsultReferral {
            id: referral.id,
            referred_for: referral.referred_for,
            referral_notes: referral.referral_notes,
            referral_state: referral.referral_state,
            referral_outcome: referral.referral_outcome,
            consult_id: referral.consult_id,
            created_at: referral.created_at,
        })
        .collect();
    Ok(Json(referrals))
}

/// Referral after it was opened or changed, including the consult's doctor and patient.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ReferralSummary {
    pub id: i32,
    pub referred_for: String,
    pub referral_notes: Option<String>,
    pub referral_state: ReferralState,
    pub referral_outcome: Option<String>,
    pub consult_id: i32,
    pub created_at: Ts,
    pub consult_date: Ts,
    pub doctor_id: Uuid,
    pub patient_id: i32,
    pub patient_name: String,
    pub doctor_nickname: String,
}

async fn summary(conn: &mut PgConnection, id: i32) -> ApiResult<ReferralSummary> {
    let row = find(conn, id).await?;
    Ok(ReferralSummary {
        id: row.referral.id,
        referred_for: row.referral.referred_for,
        referral_notes: row.referral.referral_notes,
        referral_state: row.referral.referral_state,
        referral_outcome: row.referral.referral_outcome,
        consult_id: row.referral.consult_id,
        created_at: row.referral.created_at,
        consult_date: row.consult_date,
        doctor_id: row.doctor_id,
        patient_id: row.patient_id,
        patient_name: row.patient_name,
        doctor_nickname: row.doctor_nickname,
    })
}

/// Referral to open against a consult. State starts as new.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateRequest {
    #[serde(default)]
    pub consult_id: i32,
    #[serde(default)]
    pub referred_for: String,
    pub referral_notes: Option<String>,
}

impl Validate for CreateRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("consultId", self.consult_id, 0);
        v.not_empty("referredFor", &self.referred_for);
    }
}

/// Open a referral for a consult.
async fn create(ctx: Ctx, _: Staff, Req(req): Req<CreateRequest>) -> ApiResult<Created<ReferralSummary>> {
    let mut tx = ctx.begin().await?;
    consult_exists(&mut tx, req.consult_id).await?;
    let referral = tx
        .insert(Referral {
            id: 0,
            referred_for: req.referred_for.trim().to_owned(),
            referral_notes: req.referral_notes,
            referral_state: ReferralState::New,
            referral_outcome: None,
            consult_id: req.consult_id,
            created_at: Ts::now(),
        })
        .await?;
    let created = summary(&mut tx, referral.id).await?;
    tx.commit().await?;
    Ok(Created::at(format!("/referrals/{}", created.id), created))
}

/// New referral state. An omitted outcome is not overwritten.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateStateRequest {
    #[serde(default)]
    pub id: i32,
    pub referral_state: Option<ReferralState>,
    pub referral_outcome: Option<String>,
}

impl Validate for UpdateStateRequest {
    fn validate(&self, v: &mut Rules) {
        v.not_null("referralState", &self.referral_state);
    }
}

/// Set a referral state from any prior state.
async fn update_state(ctx: Ctx, _: Staff, Req(req): Req<UpdateStateRequest>) -> ApiResult<Json<ReferralSummary>> {
    let state = req.referral_state.ok_or_else(|| ApiError::bad_request("referralState is required."))?;
    let mut tx = ctx.begin().await?;
    let before = find_referral(&mut tx, req.id).await?;
    let mut referral = before.clone();
    referral.referral_state = state;
    if req.referral_outcome.is_some() {
        referral.referral_outcome = req.referral_outcome;
    }
    let referral = tx.update(&before, referral).await?;
    let updated = summary(&mut tx, referral.id).await?;
    tx.commit().await?;
    Ok(Json(updated))
}

/// New referral outcome. The referral's state is left as it is. An empty outcome clears it.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateOutcomeRequest {
    #[serde(default)]
    pub id: i32,
    pub referral_outcome: Option<String>,
}

impl Validate for UpdateOutcomeRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("id", self.id, 0);
        v.not_null("referralOutcome", &self.referral_outcome).max_len("referralOutcome", &self.referral_outcome, 2000);
    }
}

/// Set only a referral's outcome, so it can't overwrite a newer state.
async fn update_outcome(
    ctx: Ctx,
    _: Staff,
    Req(req): Req<UpdateOutcomeRequest>,
) -> ApiResult<Json<ReferralSummary>> {
    // Only the outcome column is written, so a state change made in the meantime is
    // never overwritten.
    let mut tx = ctx.begin().await?;
    let before = find_referral(&mut tx, req.id).await?;
    let mut referral = before.clone();
    referral.referral_outcome = req.referral_outcome;
    let referral = tx.update(&before, referral).await?;
    let updated = summary(&mut tx, referral.id).await?;
    tx.commit().await?;
    Ok(Json(updated))
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/referrals", get(list).post(create))
        .route("/referrals/{id}", get(get_one))
        .route("/referrals/{id}/state", patch(update_state))
        .route("/referrals/{id}/outcome", patch(update_outcome))
        .route("/consults/{consultId}/referrals", get(list_by_consult))
}

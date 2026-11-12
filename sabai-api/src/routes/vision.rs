//! The vision station's view of a visit: who the patient is, the glasses prescribed
//! and the acuity measured.

use axum::Router;
use axum::routing::get;
use serde::Serialize;

use super::eyesight::find_by_visit;
use super::glasses::GlassesResponse;
use super::vitals::VisitRequest;
use crate::auth::Staff;
use crate::domain::{Gender, Patient, Visit};
use crate::error::{ApiResult, OrNotFound};
use crate::http::{Ctx, Json, Req};
use crate::state::AppState;
use crate::time::Ts;

/// Patient, prescribed glasses, and acuity for a visit.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct VisionResponse {
    pub patient: PatientResponse,
    pub vision: Option<GlassesResponse>,
    pub vitals: Option<AcuityResponse>,
}

/// Patient identity shown with a vision record.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PatientResponse {
    pub id: i32,
    pub name: String,
    pub display_id: String,
    pub gender: Gender,
    pub date_of_birth: Ts,
}

/// Unaided acuity and pinhole readings for a visit.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AcuityResponse {
    pub left_eye_degree: Option<String>,
    pub right_eye_degree: Option<String>,
    pub left_eye_pinhole: Option<String>,
    pub right_eye_pinhole: Option<String>,
}

/// Get the vision composite for a visit.
async fn get_vision(ctx: Ctx, _: Staff, Req(req): Req<VisitRequest>) -> ApiResult<Json<VisionResponse>> {
    let mut conn = ctx.db().acquire().await?;
    let visit: Visit = sqlx::query_as(r#"SELECT * FROM "Visits" WHERE "Id" = $1"#)
        .bind(req.visit_id)
        .fetch_optional(&mut *conn)
        .await
        .or_not_found("Visit was not found.")?;
    let patient: Patient = sqlx::query_as(r#"SELECT * FROM "Patients" WHERE "Id" = $1"#)
        .bind(visit.patient_id)
        .fetch_one(&mut *conn)
        .await?;
    // The display id carries the village of the visit, not the patient's current one.
    let village_code: String = sqlx::query_scalar(r#"SELECT "Code" FROM "VillageCodes" WHERE "Id" = $1"#)
        .bind(visit.village_code_id)
        .fetch_one(&mut *conn)
        .await?;
    let eyesight = find_by_visit(&mut conn, visit.id).await?;

    let vitals = eyesight.as_ref().map(|eyesight| AcuityResponse {
        left_eye_degree: eyesight.left_eye_degree.clone(),
        right_eye_degree: eyesight.right_eye_degree.clone(),
        left_eye_pinhole: eyesight.left_eye_pinhole.clone(),
        right_eye_pinhole: eyesight.right_eye_pinhole.clone(),
    });
    Ok(Json(VisionResponse {
        patient: PatientResponse {
            id: patient.id,
            display_id: Patient::format_display_id(Some(&village_code), patient.id),
            name: patient.name,
            gender: patient.gender,
            date_of_birth: patient.date_of_birth,
        },
        vision: eyesight.map(Into::into),
        vitals,
    }))
}

pub fn router() -> Router<AppState> {
    Router::new().route("/vision", get(get_vision))
}

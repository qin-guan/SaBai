//! Persons: faces enrolled under a free-form person id.

use axum::Router;
use axum::http::StatusCode;
use axum::response::{IntoResponse, Response};
use axum::routing::{delete, get};
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::faces::{FaceDto, Upload, no_usable_face};
use crate::auth::Staff;
use crate::error::{ApiError, ApiResult};
use crate::face::{self, StoredFace};
use crate::http::{Ctx, Json, NoContent, Req, Rules, Validate};
use crate::state::AppState;

const MAX_LENGTH: usize = 128;

fn validate_person_id(rules: &mut Rules, person_id: &str) {
    let ok = !person_id.trim().is_empty() && person_id.encode_utf16().count() <= MAX_LENGTH;
    rules.must("personId", ok, format!("PersonId must be 1-{MAX_LENGTH} characters."));
}

/// Identifies a person.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct PersonRequest {
    #[serde(default)]
    person_id: String,
}

impl Validate for PersonRequest {
    fn validate(&self, v: &mut Rules) {
        validate_person_id(v, &self.person_id);
    }
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct EnrollResponse {
    face_id: Uuid,
    person_id: String,
    face: FaceDto,
    faces_detected: usize,
}

/// Enroll a face for a person.
///
/// Uses the most prominent face in the image. Enroll several photos per person for better accuracy.
async fn enroll(ctx: Ctx, _: Staff, upload: Upload) -> ApiResult<Response> {
    upload.expect_files(&["image"])?;
    let person_id = upload.form.text("personId").unwrap_or_default().to_owned();
    let mut rules = Rules::new();
    validate_person_id(&mut rules, &person_id);
    let image = upload.image("image", &mut rules).await?;
    rules.into_result()?;
    let image = image.ok_or_else(|| ApiError::internal("the image was not loaded"))?;

    let faces = ctx.state.faces.analyze(image).await?;
    let face = face::largest(&faces).ok_or_else(|| no_usable_face(&["image"]))?;

    let stored = ctx.state.faces.add(&person_id, &face.embedding).await?;
    let body = EnrollResponse {
        face_id: stored.face_id,
        person_id: stored.person_id,
        face: FaceDto::from(&face.detection),
        faces_detected: faces.len(),
    };
    Ok((StatusCode::CREATED, Json(body)).into_response())
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct ListResponse {
    person_id: String,
    faces: Vec<StoredFace>,
}

/// List the faces enrolled for a person.
async fn list(ctx: Ctx, _: Staff, Req(req): Req<PersonRequest>) -> ApiResult<Json<ListResponse>> {
    let faces = ctx.state.faces.list(&req.person_id).await?;
    Ok(Json(ListResponse { person_id: req.person_id, faces }))
}

/// Delete every face enrolled for a person.
async fn delete_person(ctx: Ctx, _: Staff, Req(req): Req<PersonRequest>) -> ApiResult<Response> {
    if ctx.state.faces.delete_person(&req.person_id).await? > 0 {
        Ok(NoContent.into_response())
    } else {
        Ok(StatusCode::NOT_FOUND.into_response())
    }
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/persons/{personId}/faces", get(list).post(enroll))
        .route("/persons/{personId}", delete(delete_person))
}

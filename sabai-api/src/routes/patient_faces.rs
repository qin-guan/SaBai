//! Patient faces: enrolling a patient's face and finding patients by a face photo.

use std::collections::HashMap;

use axum::Router;
use axum::http::StatusCode;
use axum::response::{IntoResponse, Response};
use axum::routing::{delete, get, post};
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::faces::{FaceDto, SearchOptions, Upload, no_usable_face, number};
use crate::auth::Staff;
use crate::db::Pool;
use crate::domain::Patient;
use crate::error::{ApiError, ApiResult, OrNotFound};
use crate::face::{self, StoredFace};
use crate::http::{Ctx, Json, NoContent, Req, Rules, Validate};
use crate::state::AppState;

async fn find_patient(db: &Pool, id: i32) -> ApiResult<Patient> {
    sqlx::query_as(r#"SELECT * FROM "Patients" WHERE "Id" = $1"#)
        .bind(id)
        .fetch_optional(db)
        .await
        .or_not_found("Patient was not found.")
}

/// Identifies a patient.
#[derive(Debug, Deserialize)]
struct PatientRequest {
    #[serde(default)]
    id: i32,
}

impl Validate for PatientRequest {}

/// A face newly enrolled for a patient.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct EnrollResponse {
    face_id: Uuid,
    patient_id: i32,
    face: FaceDto,
    faces_detected: usize,
}

/// Enroll a face for a patient.
///
/// Uses the most prominent face in the image. Enroll several photos per patient for better accuracy.
async fn enroll(ctx: Ctx, _: Staff, upload: Upload) -> ApiResult<Response> {
    let req: PatientRequest = upload.form.bind()?;
    upload.expect_files(&["image"])?;
    let patient = find_patient(ctx.db(), req.id).await?;

    let mut rules = Rules::new();
    let image = upload.image("image", &mut rules).await?;
    rules.into_result()?;
    let image = image.ok_or_else(|| ApiError::internal("the image was not loaded"))?;

    let faces = ctx.state.faces.analyze(image).await?;
    let face = face::largest(&faces).ok_or_else(|| no_usable_face(&["image"]))?;

    let stored = ctx.state.faces.add(&crate::routes::patients::face_person_id(patient.id), &face.embedding).await?;

    let mut tx = ctx.begin().await?;
    let mut updated = patient.clone();
    updated.latest_face_id = Some(stored.face_id.to_string());
    tx.update(&patient, updated).await?;
    tx.commit().await?;

    let body = EnrollResponse {
        face_id: stored.face_id,
        patient_id: patient.id,
        face: FaceDto::from(&face.detection),
        faces_detected: faces.len(),
    };
    Ok((StatusCode::CREATED, Json(body)).into_response())
}

/// Faces enrolled for a patient.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct ListResponse {
    patient_id: i32,
    faces: Vec<StoredFace>,
}

/// List the faces enrolled for a patient
async fn list(ctx: Ctx, _: Staff, Req(req): Req<PatientRequest>) -> ApiResult<Json<ListResponse>> {
    let patient = find_patient(ctx.db(), req.id).await?;
    let faces = ctx.state.faces.list(&crate::routes::patients::face_person_id(patient.id)).await?;
    Ok(Json(ListResponse { patient_id: patient.id, faces }))
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct DeleteRequest {
    #[serde(default)]
    id: i32,
    #[serde(default)]
    face_id: Uuid,
}

impl Validate for DeleteRequest {}

/// Delete an enrolled face for a patient
async fn delete_face(ctx: Ctx, _: Staff, Req(req): Req<DeleteRequest>) -> ApiResult<NoContent> {
    let patient = find_patient(ctx.db(), req.id).await?;

    let faces = ctx.state.faces.list(&crate::routes::patients::face_person_id(patient.id)).await?;
    if faces.iter().all(|face| face.face_id != req.face_id) {
        return Err(ApiError::not_found("Face was not found for this patient."));
    }

    ctx.state.faces.delete_face(req.face_id).await?;

    if patient.latest_face_id.as_deref() == Some(req.face_id.to_string().as_str()) {
        // The most recent of the faces that remain; `faces` is oldest first.
        let latest = faces.iter().rfind(|face| face.face_id != req.face_id);
        let mut tx = ctx.begin().await?;
        let mut updated = patient.clone();
        updated.latest_face_id = latest.map(|face| face.face_id.to_string());
        tx.update(&patient, updated).await?;
        tx.commit().await?;
    }

    Ok(NoContent)
}

/// A patient that matched the search photo, with the best similarity seen.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct PatientFaceMatch {
    id: i32,
    name: String,
    display_id: String,
    village_code: Option<String>,
    village_color_hex: Option<String>,
    has_photo: bool,
    #[serde(serialize_with = "number")]
    similarity: f32,
}

#[derive(sqlx::FromRow)]
struct PatientRow {
    id: i32,
    name: String,
    photo: Option<String>,
    village_code: Option<String>,
    village_color_hex: Option<String>,
}

#[derive(Debug, Serialize)]
struct SearchResponse {
    matches: Vec<PatientFaceMatch>,
}

/// Search for patients by face photo
async fn search(ctx: Ctx, _: Staff, upload: Upload) -> ApiResult<Json<SearchResponse>> {
    upload.expect_files(&["image"])?;
    let mut rules = Rules::new();
    let (threshold, max_matches) = upload.query::<SearchOptions>()?.checked(5, &mut rules);
    let image = upload.image("image", &mut rules).await?;
    rules.into_result()?;
    let image = image.ok_or_else(|| ApiError::internal("the image was not loaded"))?;

    let faces = &ctx.state.faces;
    let threshold = threshold.unwrap_or(faces.settings.match_threshold);

    // Best similarity per patient across every detected face.
    let mut best: HashMap<i32, f32> = HashMap::new();
    for found in faces.analyze(image).await? {
        for candidate in faces.search(&found.embedding, threshold, max_matches).await? {
            if let Some(patient_id) = crate::routes::patients::patient_of_face_person(&candidate.person_id)
                && best.get(&patient_id).is_none_or(|&current| candidate.similarity > current)
            {
                best.insert(patient_id, candidate.similarity);
            }
        }
    }

    if best.is_empty() {
        return Ok(Json(SearchResponse { matches: Vec::new() }));
    }

    let ids: Vec<i32> = best.keys().copied().collect();
    let rows: Vec<PatientRow> = sqlx::query_as(
        r#"SELECT p."Id" AS id, p."Name" AS name, p."PatientImagePublicId" AS photo,
                  v."Code" AS village_code, v."ColorHex" AS village_color_hex
           FROM "Patients" p
           LEFT JOIN "VillageCodes" v ON v."Id" = p."VillageCodeId"
           WHERE p."Id" = ANY($1)"#,
    )
    .bind(&ids)
    .fetch_all(ctx.db())
    .await?;

    let mut matches: Vec<PatientFaceMatch> = rows
        .into_iter()
        .map(|row| PatientFaceMatch {
            id: row.id,
            name: row.name,
            display_id: Patient::format_display_id(row.village_code.as_deref(), row.id),
            village_code: row.village_code,
            village_color_hex: row.village_color_hex,
            has_photo: row.photo.is_some(),
            similarity: best[&row.id],
        })
        .collect();
    matches.sort_by(|a, b| b.similarity.partial_cmp(&a.similarity).unwrap_or(std::cmp::Ordering::Equal));
    matches.truncate(max_matches as usize);

    Ok(Json(SearchResponse { matches }))
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/patients/search-by-face", post(search))
        .route("/patients/{id}/faces", get(list).post(enroll))
        .route("/patients/{id}/faces/{faceId}", delete(delete_face))
}

#[cfg(test)]
mod tests {

    #[test]
    fn patient_identities_are_namespaced() {
        assert_eq!(crate::routes::patients::face_person_id(42), "patient:42");
        assert_eq!(crate::routes::patients::patient_of_face_person("patient:42"), Some(42));
        assert_eq!(crate::routes::patients::patient_of_face_person("patient:"), None);
        assert_eq!(crate::routes::patients::patient_of_face_person("patient:abc"), None);
        assert_eq!(crate::routes::patients::patient_of_face_person("42"), None);
        assert_eq!(crate::routes::patients::patient_of_face_person("Patient:42"), None);
    }
}

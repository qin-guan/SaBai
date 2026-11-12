//! Faces: detection, 1:1 verification and identification against the enrolled people.
//!
//! Also home to what every face endpoint shares: the response shapes for a face and
//! [`Upload`], which reads the photo(s) of a `multipart/form-data` request.

use std::sync::Arc;

use axum::Router;
use axum::extract::{FromRequest, Request};
use axum::http::{StatusCode, header};
use axum::response::{IntoResponse, Response};
use axum::routing::{delete, post};
use serde::de::DeserializeOwned;
use serde::{Deserialize, Serialize, Serializer};
use serde_json::{Map, Value};
use uuid::Uuid;

use crate::auth::Staff;
use crate::error::{ApiError, ApiResult, FieldError};
use crate::face::{self, AnalyzedFace, DetectedFace, FaceMatch, RgbImage, embedder};
use crate::http::{Ctx, Form, Json, NoContent, Req, Rules, Validate, lenient};
use crate::state::AppState;

/// Photos with more pixels than this are refused.
const MAX_PIXELS: usize = 50_000_000;

pub(crate) const NO_USABLE_FACE: &str = "No usable face detected. Use a closer, sharp, well-lit photo.";

/// Whole numbers are written without a fraction (`112`, not `112.0`), as they always were.
pub(crate) fn number<S: Serializer>(value: &f32, serializer: S) -> Result<S::Ok, S::Error> {
    if value.fract() == 0.0 && value.abs() < 1e15 {
        serializer.serialize_i64(*value as i64)
    } else {
        serializer.serialize_f32(*value)
    }
}

#[derive(Debug, Serialize)]
pub struct BoxDto {
    #[serde(serialize_with = "number")]
    pub x: f32,
    #[serde(serialize_with = "number")]
    pub y: f32,
    #[serde(serialize_with = "number")]
    pub width: f32,
    #[serde(serialize_with = "number")]
    pub height: f32,
}

#[derive(Debug, Serialize)]
pub struct PointDto {
    #[serde(serialize_with = "number")]
    pub x: f32,
    #[serde(serialize_with = "number")]
    pub y: f32,
}

/// A face found in a photo: its box, the detector's confidence and the five landmarks.
#[derive(Debug, Serialize)]
pub struct FaceDto {
    pub r#box: BoxDto,
    #[serde(serialize_with = "number")]
    pub score: f32,
    pub landmarks: Vec<PointDto>,
}

impl From<&DetectedFace> for FaceDto {
    fn from(face: &DetectedFace) -> Self {
        FaceDto {
            r#box: BoxDto { x: face.bbox.x1, y: face.bbox.y1, width: face.bbox.width(), height: face.bbox.height() },
            score: face.score,
            landmarks: face.landmarks.iter().map(|p| PointDto { x: p.x, y: p.y }).collect(),
        }
    }
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct MatchDto {
    pub person_id: String,
    pub face_id: Uuid,
    #[serde(serialize_with = "number")]
    pub similarity: f32,
}

impl From<FaceMatch> for MatchDto {
    fn from(found: FaceMatch) -> Self {
        MatchDto { person_id: found.person_id, face_id: found.face_id, similarity: found.similarity }
    }
}

/// A `multipart/form-data` request carrying one or more photos. Anything else is
/// answered with 415.
pub(crate) struct Upload {
    pub form: Form,
    query: Option<String>,
}

impl<S: Send + Sync> FromRequest<S> for Upload {
    type Rejection = ApiError;

    async fn from_request(request: Request, state: &S) -> Result<Self, ApiError> {
        let is_multipart = request
            .headers()
            .get(header::CONTENT_TYPE)
            .and_then(|value| value.to_str().ok())
            .is_some_and(|value| value.split(';').next().unwrap_or("").trim().eq_ignore_ascii_case("multipart/form-data"));
        if !is_multipart {
            return Err(ApiError::UnsupportedMediaType);
        }
        let query = request.uri().query().map(str::to_owned);
        let form = Form::from_request(request, state).await?;
        Ok(Upload { form, query })
    }
}

impl Upload {
    /// Options that are read from the query string only, never from form fields.
    pub fn query<T: DeserializeOwned>(&self) -> ApiResult<T> {
        let mut values: Map<String, Value> = Map::new();
        for (key, value) in url::form_urlencoded::parse(self.query.as_deref().unwrap_or("").as_bytes()) {
            let existing = values.iter_mut().find(|(name, _)| name.eq_ignore_ascii_case(&key));
            match existing {
                // A repeated option reads as its values joined by commas.
                Some((_, Value::String(text))) => {
                    text.push(',');
                    text.push_str(&value);
                }
                _ => {
                    values.insert(key.into_owned(), Value::String(value.into_owned()));
                }
            }
        }
        lenient::from_value(&Value::Object(values)).map_err(|error| ApiError::binding(error.name(), error.message))
    }

    /// Fails when a photo field was sent as plain text instead of a file.
    pub fn expect_files(&self, fields: &[&str]) -> ApiResult<()> {
        for field in fields {
            if self.form.file(field).is_none()
                && let Some(text) = self.form.text(field)
            {
                return Err(ApiError::binding(*field, format!("Value [{text}] is not valid for a [IFormFile] property!")));
            }
        }
        Ok(())
    }

    /// Decodes the photo sent as `field`, recording a validation error on failure.
    pub async fn image(&self, field: &str, rules: &mut Rules) -> ApiResult<Option<Arc<RgbImage>>> {
        let Some(file) = self.form.file(field).filter(|file| !file.is_empty()) else {
            rules.add(field, "An image file is required.");
            return Ok(None);
        };
        let bytes = file.bytes.clone();
        let decoded = tokio::task::spawn_blocking(move || RgbImage::decode(&bytes)).await.map_err(ApiError::internal)?;
        Ok(match decoded {
            Ok(image) if image.width * image.height > MAX_PIXELS => {
                rules.add(field, "Image resolution is too large.");
                None
            }
            Ok(image) => Some(Arc::new(image)),
            Err(error) => {
                rules.add(field, error.0);
                None
            }
        })
    }
}

/// `422` naming the photo fields in which no usable face was found.
pub(crate) fn no_usable_face(fields: &[&str]) -> ApiError {
    ApiError::Unprocessable { errors: fields.iter().map(|field| FieldError::new(*field, NO_USABLE_FACE)).collect() }
}

/// How many matches to return and how similar they must be.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct SearchOptions {
    /// Minimum cosine similarity for a match. Defaults to the configured match threshold.
    pub threshold: Option<f32>,
    /// Maximum number of candidates returned.
    pub max_matches: Option<i32>,
}

impl SearchOptions {
    /// The options with `default_max_matches` filled in, recording out-of-range values.
    pub fn checked(self, default_max_matches: i32, rules: &mut Rules) -> (Option<f32>, i32) {
        let max_matches = self.max_matches.unwrap_or(default_max_matches);
        if self.threshold.is_some_and(|threshold| !(-1.0..=1.0).contains(&threshold)) {
            rules.add("threshold", "Threshold must be between -1 and 1.");
        }
        if !(1..=20).contains(&max_matches) {
            rules.add("maxMatches", "MaxMatches must be between 1 and 20.");
        }
        (self.threshold, max_matches)
    }
}

#[derive(Debug, Serialize)]
struct DetectResponse {
    width: usize,
    height: usize,
    faces: Vec<FaceDto>,
}

/// Detect faces and their five-point landmarks in an image.
async fn detect(ctx: Ctx, _: Staff, upload: Upload) -> ApiResult<Json<DetectResponse>> {
    upload.expect_files(&["image"])?;
    let mut rules = Rules::new();
    let image = upload.image("image", &mut rules).await?;
    rules.into_result()?;
    let image = image.ok_or_else(|| ApiError::internal("the image was not loaded"))?;

    let faces = ctx.state.faces.detect(image.clone()).await?;
    Ok(Json(DetectResponse {
        width: image.width,
        height: image.height,
        faces: faces.iter().map(FaceDto::from).collect(),
    }))
}

#[derive(Debug, Serialize)]
struct IdentifiedFace {
    face: FaceDto,
    matches: Vec<MatchDto>,
}

#[derive(Debug, Serialize)]
struct IdentifyResponse {
    faces: Vec<IdentifiedFace>,
}

/// Identify every face in an image against the enrolled people.
async fn identify(ctx: Ctx, _: Staff, upload: Upload) -> ApiResult<Json<IdentifyResponse>> {
    upload.expect_files(&["image"])?;
    let mut rules = Rules::new();
    let (threshold, max_matches) = upload.query::<SearchOptions>()?.checked(1, &mut rules);
    let image = upload.image("image", &mut rules).await?;
    rules.into_result()?;
    let image = image.ok_or_else(|| ApiError::internal("the image was not loaded"))?;

    let faces = &ctx.state.faces;
    let threshold = threshold.unwrap_or(faces.settings.match_threshold);
    let mut results = Vec::new();
    for found in faces.analyze(image).await? {
        let matches = faces.search(&found.embedding, threshold, max_matches).await?;
        results.push(IdentifiedFace {
            face: FaceDto::from(&found.detection),
            matches: matches.into_iter().map(MatchDto::from).collect(),
        });
    }
    Ok(Json(IdentifyResponse { faces: results }))
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct VerifyResponse {
    #[serde(serialize_with = "number")]
    similarity: f32,
    is_match: bool,
    #[serde(serialize_with = "number")]
    threshold: f32,
    first: FaceDto,
    second: FaceDto,
}

/// Compare the most prominent face in two images (1:1 verification).
async fn verify(ctx: Ctx, _: Staff, upload: Upload) -> ApiResult<Json<VerifyResponse>> {
    upload.expect_files(&["first", "second"])?;
    let mut rules = Rules::new();
    let first = upload.image("first", &mut rules).await?;
    let second = upload.image("second", &mut rules).await?;
    rules.into_result()?;
    let (Some(first), Some(second)) = (first, second) else {
        return Err(ApiError::internal("the images were not loaded"));
    };

    let faces = &ctx.state.faces;
    let first_faces = faces.analyze(first).await?;
    let second_faces = faces.analyze(second).await?;
    let (a, b): (&AnalyzedFace, &AnalyzedFace) = match (face::largest(&first_faces), face::largest(&second_faces)) {
        (Some(a), Some(b)) => (a, b),
        (a, b) => {
            let mut missing = Vec::new();
            if a.is_none() {
                missing.push("first");
            }
            if b.is_none() {
                missing.push("second");
            }
            return Err(no_usable_face(&missing));
        }
    };

    let similarity = embedder::similarity(&a.embedding, &b.embedding);
    let threshold = faces.settings.match_threshold;
    Ok(Json(VerifyResponse {
        similarity,
        is_match: similarity >= threshold,
        threshold,
        first: FaceDto::from(&a.detection),
        second: FaceDto::from(&b.detection),
    }))
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct DeleteRequest {
    #[serde(default)]
    face_id: Uuid,
}

impl Validate for DeleteRequest {}

/// Delete a single enrolled face.
async fn delete_face(ctx: Ctx, _: Staff, Req(req): Req<DeleteRequest>) -> ApiResult<Response> {
    if ctx.state.faces.delete_face(req.face_id).await? {
        Ok(NoContent.into_response())
    } else {
        Ok(StatusCode::NOT_FOUND.into_response())
    }
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/faces/detect", post(detect))
        .route("/faces/identify", post(identify))
        .route("/faces/verify", post(verify))
        .route("/faces/{faceId}", delete(delete_face))
}

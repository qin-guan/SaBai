//! A single profile photo per patient, kept in the RustFS bucket.

use axum::Router;
use axum::body::Bytes;
use axum::extract::DefaultBodyLimit;
use axum::http::HeaderMap;
use axum::response::Response;
use axum::routing::get;
use serde::Serialize;
use uuid::Uuid;

use super::{IdRequest, find};
use crate::auth::Staff;
use crate::error::{ApiError, ApiResult};
use crate::face::RgbImage;
use crate::http::request::{Form, UploadedFile};
use crate::http::{Ctx, Json, NoContent, Req, Rules};
use crate::routes::files::{base_name, download, extension, multipart, uploads};
use crate::state::AppState;

/// Maximum accepted photo size.
pub const MAX_BYTES: usize = 10 * 1024 * 1024;

const MAX_PIXELS: usize = 50_000_000;

pub fn build_key(patient_id: i32, file_name: &str) -> String {
    format!("patients/{patient_id}/{}{}", Uuid::new_v4().simple(), photo_extension(file_name))
}

/// The extension a photo is stored under: the upload's own, cleaned up, or `.jpg`.
pub fn photo_extension(file_name: &str) -> String {
    let cleaned: String = extension(base_name(file_name)).chars().filter(|c| c.is_alphanumeric()).collect();
    if cleaned.is_empty() { ".jpg".to_owned() } else { format!(".{}", cleaned.to_lowercase()) }
}

pub fn content_type<'a>(content_type: &'a str, extension: &str) -> &'a str {
    if !content_type.trim().is_empty() {
        return content_type;
    }
    match extension {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        _ => "image/jpeg",
    }
}

fn invalid(property: &str, reason: impl Into<String>) -> ApiError {
    let mut rules = Rules::new();
    rules.add(property, reason);
    rules.into_result().expect_err("a failure was added")
}

/// Checks that the upload decodes as an image and returns the raw bytes to store.
pub async fn read_validated(file: Option<&UploadedFile>, property: &str) -> ApiResult<Bytes> {
    let Some(file) = file.filter(|file| !file.is_empty()) else {
        return Err(invalid(property, "An image file is required."));
    };
    if file.len() > MAX_BYTES {
        return Err(invalid(property, "Image is too large."));
    }

    let bytes = file.bytes.clone();
    let decoded = tokio::task::spawn_blocking(move || RgbImage::decode(&bytes).map(|image| image.width * image.height))
        .await
        .map_err(ApiError::internal)?;
    match decoded {
        Ok(pixels) if pixels > MAX_PIXELS => Err(invalid(property, "Image resolution is too large.")),
        Ok(_) => Ok(file.bytes.clone()),
        Err(error) => Err(invalid(property, error.0)),
    }
}

/// Best-effort removal of a stored object; failures are logged, never returned.
pub async fn delete_object(ctx: &Ctx, key: Option<&str>) {
    let Some(key) = key.filter(|key| !key.trim().is_empty()) else {
        return;
    };
    if let Err(error) = ctx.state.storage.delete(key).await {
        tracing::warn!(error = format!("{error:#}"), key, "Failed to delete patient photo object");
    }
}

/// Download a patient's profile photo.
async fn get_photo(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<Response> {
    let mut conn = ctx.db().acquire().await?;
    let patient = find(&mut conn, req.id).await?;
    drop(conn);

    let Some(key) = patient.photo_stored_file_name.as_deref().filter(|key| !key.trim().is_empty()) else {
        return Err(ApiError::not_found("Patient has no photo."));
    };
    let object =
        ctx.state.storage.get(key).await?.ok_or_else(|| ApiError::not_found("Patient photo was not found."))?;
    let length = object.content_length;
    Ok(download(object, &format!("patient-{}{}", patient.id, extension(key)), length))
}

/// Result of setting a patient photo.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PutResponse {
    pub id: i32,
    pub has_photo: bool,
}

/// Set or replace a patient's profile photo.
async fn put_photo(ctx: Ctx, _: Staff, headers: HeaderMap, form: ApiResult<Form>) -> ApiResult<Json<PutResponse>> {
    let form = multipart(&headers, form)?;
    let req: IdRequest = form.bind()?;

    let mut tx = ctx.begin().await?;
    let before = find(&mut tx, req.id).await?;

    let image = uploads(&form).find(|file| file.field.eq_ignore_ascii_case("image"));
    let bytes = read_validated(image, "image").await?;
    let image = image.ok_or_else(|| ApiError::internal("the validated image is missing"))?;

    let extension = photo_extension(&image.file_name);
    let key = build_key(before.id, &image.file_name);
    ctx.state.storage.put(&key, bytes, content_type(&image.content_type, &extension)).await?;

    let mut patient = before.clone();
    patient.photo_stored_file_name = Some(key.clone());
    let patient = tx.update(&before, patient).await?;
    tx.commit().await?;

    if before.photo_stored_file_name.as_deref() != Some(key.as_str()) {
        delete_object(&ctx, before.photo_stored_file_name.as_deref()).await;
    }
    Ok(Json(PutResponse { id: patient.id, has_photo: true }))
}

/// Remove a patient's profile photo.
async fn delete_photo(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<NoContent> {
    let mut tx = ctx.begin().await?;
    let before = find(&mut tx, req.id).await?;
    let mut patient = before.clone();
    patient.photo_stored_file_name = None;
    tx.update(&before, patient).await?;
    tx.commit().await?;

    delete_object(&ctx, before.photo_stored_file_name.as_deref()).await;
    Ok(NoContent)
}

pub fn router() -> Router<AppState> {
    let upload_limit = DefaultBodyLimit::max(MAX_BYTES + 1024);
    Router::new().route("/patients/{id}/photo", get(get_photo).delete(delete_photo).merge(axum::routing::put(put_photo).layer(upload_limit)))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn extensions_and_content_types() {
        assert_eq!(photo_extension("My Photo.PNG"), ".png");
        assert_eq!(photo_extension("photo"), ".jpg");
        assert_eq!(photo_extension("dir.x/photo.j-p_g"), ".jpg");
        assert_eq!(photo_extension("a.tar.we!bp"), ".webp");
        assert_eq!(content_type("image/heic", ".png"), "image/heic");
        assert_eq!(content_type(" ", ".png"), "image/png");
        assert_eq!(content_type("", ".jpeg"), "image/jpeg");
    }
}

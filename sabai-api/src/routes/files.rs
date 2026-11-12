//! Clinical files: patient documents and images kept in the RustFS bucket.

use axum::Router;
use axum::body::Body;
use axum::extract::DefaultBodyLimit;
use axum::http::{HeaderMap, HeaderValue, header};
use axum::response::{IntoResponse, Response};
use axum::routing::{get, post};
use serde::{Deserialize, Serialize};
use sqlx::PgConnection;
use uuid::Uuid;

use crate::auth::Staff;
use crate::domain::StoredFile;
use crate::error::{ApiError, ApiResult, OrNotFound};
use crate::http::request::{Form, UploadedFile};
use crate::http::{Created, Ctx, Json, Req, Rules, Validate};
use crate::state::AppState;
use crate::storage::StoredObject;
use crate::time::Ts;

const NOT_FOUND: &str = "File was not found.";

/// The part of a path after the last `/`.
pub(crate) fn base_name(path: &str) -> &str {
    path.rsplit('/').next().unwrap_or(path)
}

/// The extension of a file name with its dot (`.pdf`), or nothing when it has none.
pub(crate) fn extension(file_name: &str) -> &str {
    let name = base_name(file_name);
    match name.rfind('.') {
        Some(dot) if dot + 1 < name.len() => &name[dot..],
        _ => "",
    }
}

fn is_token_char(c: char) -> bool {
    c.is_ascii_alphanumeric() || "!#$%&'*+-.^_`|~".contains(c)
}

/// `attachment; filename=…; filename*=UTF-8''…`: a plain name for old clients (quoted when
/// needed, characters outside ASCII replaced by `_`) and the exact name percent-encoded.
pub(crate) fn attachment(file_name: &str) -> String {
    let mut plain: String = if file_name.chars().any(|c| !(' '..='\u{7f}').contains(&c)) {
        file_name
            .encode_utf16()
            .map(|unit| match char::from_u32(u32::from(unit)) {
                Some(c) if (' '..='\u{7f}').contains(&c) => c,
                _ => '_',
            })
            .collect()
    } else {
        file_name.to_owned()
    };
    let mut quote = false;
    if plain.len() > 1 && plain.starts_with('"') && plain.ends_with('"') {
        plain = plain[1..plain.len() - 1].to_owned();
        quote = true;
    }
    if quote || !plain.chars().all(is_token_char) {
        plain = format!("\"{}\"", plain.replace('\\', "\\\\").replace('"', "\\\""));
    }

    let mut exact = String::new();
    for byte in file_name.bytes() {
        let c = char::from(byte);
        if byte < 0x80 && is_token_char(c) && !matches!(c, '*' | '\'' | '%') {
            exact.push(c);
        } else {
            exact.push_str(&format!("%{byte:02X}"));
        }
    }
    format!("attachment; filename={plain}; filename*=UTF-8''{exact}")
}

/// Sends a stored object as a download named `file_name`.
pub(crate) fn download(object: StoredObject, file_name: &str, length: Option<u64>) -> Response {
    let content_type = object
        .content_type
        .as_deref()
        .filter(|value| !value.trim().is_empty())
        .unwrap_or("application/octet-stream")
        .to_owned();
    let mut headers = HeaderMap::new();
    if let Ok(value) = HeaderValue::from_str(&content_type) {
        headers.insert(header::CONTENT_TYPE, value);
    }
    if let Ok(value) = HeaderValue::from_str(&attachment(file_name)) {
        headers.insert(header::CONTENT_DISPOSITION, value);
    }
    if let Some(length) = length {
        headers.insert(header::CONTENT_LENGTH, HeaderValue::from(length));
    }
    (headers, Body::from_stream(object.into_stream())).into_response()
}

/// The form of an upload request. Anything but `multipart/form-data` is refused, and a
/// form that cannot be read (cut short, or larger than the endpoint allows) is malformed.
pub(crate) fn multipart(headers: &HeaderMap, form: ApiResult<Form>) -> ApiResult<Form> {
    let is_multipart = headers
        .get(header::CONTENT_TYPE)
        .and_then(|value| value.to_str().ok())
        .is_some_and(|value| value.trim_start().to_ascii_lowercase().starts_with("multipart/form-data"));
    if !is_multipart {
        return Err(ApiError::UnsupportedMediaType);
    }
    form.map_err(|error| match error {
        ApiError::PayloadTooLarge | ApiError::Validation { .. } => {
            ApiError::binding("generalErrors", "The request body contains malformed form data!")
        }
        other => other,
    })
}

/// The files of a form. A part without a file name is a text field, not a file.
pub(crate) fn uploads(form: &Form) -> impl Iterator<Item = &UploadedFile> {
    form.files.iter().filter(|file| !file.file_name.is_empty())
}

/// Clinical file metadata. Bytes are stored in the RustFS bucket.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct FileResponse {
    pub id: i32,
    pub patient_id: Option<i32>,
    pub file_name: String,
    pub stored_file_name: String,
    pub description: String,
    pub size_bytes: i64,
    pub is_deleted: bool,
    pub created_at: Ts,
}

impl From<StoredFile> for FileResponse {
    fn from(file: StoredFile) -> Self {
        FileResponse {
            id: file.id,
            patient_id: file.patient_id,
            file_name: file.file_name,
            stored_file_name: file.stored_file_name,
            description: file.description,
            size_bytes: file.size_bytes,
            is_deleted: file.is_deleted,
            created_at: file.created_at,
        }
    }
}

/// Identifies a clinical file.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct IdRequest {
    #[serde(default)]
    pub id: i32,
}

impl Validate for IdRequest {}

async fn find(conn: &mut PgConnection, id: i32) -> ApiResult<StoredFile> {
    sqlx::query_as(r#"SELECT * FROM "Files" WHERE "Id" = $1"#).bind(id).fetch_optional(conn).await.or_not_found(NOT_FOUND)
}

/// Multipart upload of one or more clinical files for a patient.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateRequest {
    #[serde(default)]
    pub patient_id: i32,
}

impl Validate for CreateRequest {}

/// Upload clinical files to RustFS.
async fn create(ctx: Ctx, _: Staff, headers: HeaderMap, form: ApiResult<Form>) -> ApiResult<Created<Vec<FileResponse>>> {
    let form = multipart(&headers, form)?;
    let req: CreateRequest = form.bind()?;

    let patient_exists: bool = sqlx::query_scalar(r#"SELECT EXISTS (SELECT 1 FROM "Patients" WHERE "Id" = $1)"#)
        .bind(req.patient_id)
        .fetch_one(ctx.db())
        .await?;
    if !patient_exists {
        return Err(ApiError::bad_request("Patient was not found."));
    }

    let mut files: Vec<&UploadedFile> = uploads(&form).filter(|file| file.field.eq_ignore_ascii_case("files")).collect();
    if files.is_empty() {
        files = uploads(&form).collect();
    }
    let descriptions = form.texts("descriptions");
    if files.is_empty() {
        return Err(ApiError::bad_request("No file was uploaded."));
    }
    if files.len() != descriptions.len() {
        return Err(ApiError::bad_request("Number of files and descriptions must match."));
    }

    // Objects already in the bucket are removed again when a later file is refused or
    // the records cannot be saved.
    let mut uploaded_keys = Vec::new();
    match store(&ctx, req.patient_id, &files, &descriptions, &mut uploaded_keys).await {
        Ok(created) => {
            let location = format!("/files/{}", created[0].id);
            Ok(Created::at(location, created.into_iter().map(Into::into).collect()))
        }
        Err(error) => {
            for key in &uploaded_keys {
                if let Err(failure) = ctx.state.storage.delete(key).await {
                    tracing::warn!(error = format!("{failure:#}"), key, "Failed to remove an uploaded file");
                }
            }
            Err(error)
        }
    }
}

async fn store(
    ctx: &Ctx,
    patient_id: i32,
    files: &[&UploadedFile],
    descriptions: &[&str],
    uploaded_keys: &mut Vec<String>,
) -> ApiResult<Vec<StoredFile>> {
    let mut tx = ctx.begin().await?;
    let mut created = Vec::with_capacity(files.len());
    for (upload, description) in files.iter().zip(descriptions) {
        let file_name = base_name(&upload.file_name);
        if file_name.trim().is_empty() {
            return Err(ApiError::bad_request("No name provided."));
        }
        if file_name.encode_utf16().count() > 255 {
            return Err(ApiError::bad_request("File name is too long."));
        }
        if upload.is_empty() {
            return Err(ApiError::bad_request(format!("{file_name} - File is empty.")));
        }
        if upload.len() as i64 > StoredFile::MAX_BYTES {
            return Err(ApiError::bad_request(format!("{file_name} - File is too large.")));
        }
        if !StoredFile::is_allowed_content_type(&upload.content_type) {
            return Err(ApiError::bad_request(format!(
                "{file_name} - Only PDF, JPEG, PNG and DOCX files are allowed."
            )));
        }

        let stored_name = format!("{}{}", Uuid::new_v4().simple(), extension(file_name));
        ctx.state.storage.put(&stored_name, upload.bytes.clone(), &upload.content_type).await?;
        uploaded_keys.push(stored_name.clone());

        let file = tx
            .insert(StoredFile {
                id: 0,
                patient_id: Some(patient_id),
                file_name: file_name.to_owned(),
                stored_file_name: stored_name,
                description: (*description).to_owned(),
                size_bytes: upload.len() as i64,
                is_deleted: false,
                created_at: Ts::now(),
            })
            .await?;
        created.push(file);
    }
    tx.commit().await?;
    Ok(created)
}

/// File list filters. Deleted defaults to non-deleted files.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ListRequest {
    pub patient_id: Option<i32>,
    pub deleted: Option<String>,
}

impl Validate for ListRequest {}

/// List clinical files.
async fn list(ctx: Ctx, _: Staff, Req(req): Req<ListRequest>) -> ApiResult<Json<Vec<FileResponse>>> {
    let deleted = req.deleted.as_deref().unwrap_or("false").trim().to_lowercase();
    let is_deleted = match deleted.as_str() {
        "false" => Some(false),
        "true" => Some(true),
        "all" => None,
        _ => return Err(ApiError::bad_request("deleted parameter must be 'all', 'true', or 'false'.")),
    };

    let files: Vec<StoredFile> = sqlx::query_as(
        r#"SELECT * FROM "Files"
           WHERE ($1::int IS NULL OR "PatientId" = $1) AND ($2::bool IS NULL OR "IsDeleted" = $2)
           ORDER BY "CreatedAt" DESC, "Id" DESC"#,
    )
    .bind(req.patient_id)
    .bind(is_deleted)
    .fetch_all(ctx.db())
    .await?;
    Ok(Json(files.into_iter().map(Into::into).collect()))
}

/// Get a clinical file.
async fn get_one(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<Json<FileResponse>> {
    let mut conn = ctx.db().acquire().await?;
    Ok(Json(find(&mut conn, req.id).await?.into()))
}

/// Download a clinical file from RustFS.
async fn content(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<Response> {
    let file: StoredFile = sqlx::query_as(r#"SELECT * FROM "Files" WHERE "Id" = $1 AND NOT "IsDeleted""#)
        .bind(req.id)
        .fetch_optional(ctx.db())
        .await
        .or_not_found(NOT_FOUND)?;
    let object = ctx
        .state
        .storage
        .get(&file.stored_file_name)
        .await?
        .ok_or_else(|| ApiError::not_found("File content was not found."))?;
    Ok(download(object, &file.file_name, u64::try_from(file.size_bytes).ok()))
}

/// Partial clinical file metadata update. Does not call an external store.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateRequest {
    #[serde(default)]
    pub id: i32,
    pub file_name: Option<String>,
    pub description: Option<String>,
}

impl Validate for UpdateRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("id", self.id, 0);
        v.max_len("fileName", &self.file_name, 255);
    }
}

/// Update clinical file metadata.
async fn update(ctx: Ctx, _: Staff, Req(req): Req<UpdateRequest>) -> ApiResult<Json<FileResponse>> {
    let mut tx = ctx.begin().await?;
    let before = find(&mut tx, req.id).await?;
    let mut file = before.clone();

    if let Some(file_name) = &req.file_name {
        let file_name = base_name(file_name.trim());
        if file_name.trim().is_empty() {
            return Err(ApiError::bad_request("No name provided."));
        }
        file.file_name = file_name.to_owned();
    }
    if let Some(description) = req.description {
        file.description = description;
    }

    let file = tx.update(&before, file).await?;
    tx.commit().await?;
    Ok(Json(file.into()))
}

async fn set_deleted(ctx: &Ctx, id: i32, is_deleted: bool) -> ApiResult<Json<FileResponse>> {
    let mut tx = ctx.begin().await?;
    let before = find(&mut tx, id).await?;
    let mut file = before.clone();
    file.is_deleted = is_deleted;
    let file = tx.update(&before, file).await?;
    tx.commit().await?;
    Ok(Json(file.into()))
}

/// Soft-delete a clinical file.
async fn delete(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<Json<FileResponse>> {
    set_deleted(&ctx, req.id, true).await
}

/// Restore a soft-deleted clinical file.
async fn restore(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<Json<FileResponse>> {
    set_deleted(&ctx, req.id, false).await
}

pub fn router() -> Router<AppState> {
    let upload_limit = DefaultBodyLimit::max(StoredFile::MAX_BYTES as usize * 8);
    Router::new()
        .route("/files", get(list).merge(post(create).layer(upload_limit)))
        .route("/files/{id}", get(get_one).patch(update).delete(delete))
        .route("/files/{id}/content", get(content))
        .route("/files/{id}/restore", post(restore))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn names_and_extensions() {
        assert_eq!(base_name("dir/sub\\note.pdf"), "sub\\note.pdf");
        assert_eq!(base_name("dir/"), "");
        assert_eq!(extension("a/b.c/note.final.PDF"), ".PDF");
        assert_eq!(extension("noext"), "");
        assert_eq!(extension("trailing."), "");
        assert_eq!(extension(".hidden"), ".hidden");
    }

    #[test]
    fn attachment_headers_match_the_previous_api() {
        assert_eq!(attachment("patient-7.png"), "attachment; filename=patient-7.png; filename*=UTF-8''patient-7.png");
        assert_eq!(
            attachment("sub\\note.final.pdf"),
            "attachment; filename=\"sub\\\\note.final.pdf\"; filename*=UTF-8''sub%5Cnote.final.pdf"
        );
        assert_eq!(
            attachment("rapport médical %22x%22.pdf"),
            "attachment; filename=\"rapport m_dical %22x%22.pdf\"; filename*=UTF-8''rapport%20m%C3%A9dical%20%2522x%2522.pdf"
        );
    }
}

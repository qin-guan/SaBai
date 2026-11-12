//! Reading requests.
//!
//! [`Req<T>`] builds one request value from everything the client sent, the way the
//! previous API bound its request DTOs: the JSON body first, then form fields, then
//! route parameters, then the query string (later sources win), matched to the type's
//! properties in any case. It then runs the type's [`Validate`] rules. So an endpoint
//! declares one struct with `id` (from the route), filters (from the query) and body
//! fields side by side.
//!
//! [`Form`] does the same for `multipart/form-data` uploads and also hands over the files.

use axum::body::Bytes;
use axum::extract::{FromRequest, FromRequestParts, Multipart, RawPathParams, Request};
use axum::http::request::Parts;
use axum::http::{Method, header};
use serde::de::DeserializeOwned;
use serde_json::{Map, Value};

use super::lenient;
use super::validate::{Rules, Validate};
use crate::error::{ApiError, ApiResult};

/// Requests larger than this are refused (file uploads are capped at 25 MB each).
pub const MAX_BODY_BYTES: usize = 30_000_000;

/// A bound and validated request. See the module docs.
pub struct Req<T>(pub T);

/// Sets `key`, replacing an entry whose name differs only in case.
fn put(values: &mut Map<String, Value>, key: &str, value: Value) {
    let existing: Vec<String> = values.keys().filter(|k| k.eq_ignore_ascii_case(key)).cloned().collect();
    for name in existing {
        values.remove(&name);
    }
    values.insert(key.to_owned(), value);
}

/// Adds a text value from the route, query or a form; a repeated key becomes a list.
fn put_text(values: &mut Map<String, Value>, seen: &mut Vec<String>, key: &str, text: String) {
    let lower = key.to_ascii_lowercase();
    if seen.contains(&lower) {
        let name = values.keys().find(|k| k.eq_ignore_ascii_case(key)).cloned();
        if let Some(name) = name {
            match values.get_mut(&name) {
                Some(Value::Array(items)) => items.push(Value::String(text)),
                Some(existing) => *existing = Value::Array(vec![existing.take(), Value::String(text)]),
                None => {}
            }
            return;
        }
    }
    seen.push(lower);
    put(values, key, Value::String(text));
}

async fn route_and_query<S: Send + Sync>(parts: &mut Parts, state: &S, values: &mut Map<String, Value>) {
    if let Ok(params) = RawPathParams::from_request_parts(parts, state).await {
        let mut seen = Vec::new();
        for (key, value) in &params {
            put_text(values, &mut seen, key, value.to_owned());
        }
    }
    if let Some(query) = parts.uri.query() {
        let mut seen = Vec::new();
        for (key, value) in url::form_urlencoded::parse(query.as_bytes()) {
            put_text(values, &mut seen, &key, value.into_owned());
        }
    }
}

fn content_type(parts: &Parts) -> String {
    parts
        .headers
        .get(header::CONTENT_TYPE)
        .and_then(|value| value.to_str().ok())
        .map(|value| value.split(';').next().unwrap_or("").trim().to_ascii_lowercase())
        .unwrap_or_default()
}

fn is_json(content_type: &str) -> bool {
    content_type == "application/json" || content_type.ends_with("+json")
}

fn bind<T: DeserializeOwned + Validate>(values: Map<String, Value>) -> ApiResult<T> {
    let request: T = lenient::from_value(&Value::Object(values))
        .map_err(|error| ApiError::binding(error.name(), error.message))?;
    let mut rules = Rules::new();
    request.validate(&mut rules);
    rules.into_result()?;
    Ok(request)
}

impl<S, T> FromRequest<S> for Req<T>
where
    S: Send + Sync,
    T: DeserializeOwned + Validate,
{
    type Rejection = ApiError;

    async fn from_request(request: Request, state: &S) -> Result<Self, ApiError> {
        let (mut parts, body) = request.into_parts();
        let mut values = Map::new();

        let has_body = !matches!(parts.method, Method::GET | Method::HEAD | Method::OPTIONS)
            || parts.headers.contains_key(header::CONTENT_LENGTH)
            || parts.headers.contains_key(header::TRANSFER_ENCODING);
        if has_body {
            let bytes = axum::body::to_bytes(body, MAX_BODY_BYTES).await.map_err(|_| ApiError::PayloadTooLarge)?;
            if !bytes.is_empty() {
                let content_type = content_type(&parts);
                if is_json(&content_type) {
                    match serde_json::from_slice::<Value>(&bytes) {
                        Ok(Value::Object(map)) => {
                            for (key, value) in map {
                                put(&mut values, &key, value);
                            }
                        }
                        Ok(_) => {
                            return Err(ApiError::binding(
                                lenient::SERIALIZER_ERRORS,
                                "The JSON value could not be converted to the request. Path: $.",
                            ));
                        }
                        Err(error) => {
                            return Err(ApiError::binding(lenient::SERIALIZER_ERRORS, error.to_string()));
                        }
                    }
                } else if content_type == "application/x-www-form-urlencoded" {
                    let mut seen = Vec::new();
                    for (key, value) in url::form_urlencoded::parse(&bytes) {
                        put_text(&mut values, &mut seen, &key, value.into_owned());
                    }
                } else {
                    return Err(ApiError::UnsupportedMediaType);
                }
            }
        }

        route_and_query(&mut parts, state, &mut values).await;
        bind(values).map(Req)
    }
}

/// One uploaded file of a multipart form.
#[derive(Debug, Clone)]
pub struct UploadedFile {
    /// The form field it was sent under.
    pub field: String,
    pub file_name: String,
    /// As declared by the client; empty when it sent none.
    pub content_type: String,
    pub bytes: Bytes,
}

impl UploadedFile {
    pub fn len(&self) -> usize {
        self.bytes.len()
    }

    pub fn is_empty(&self) -> bool {
        self.bytes.is_empty()
    }
}

/// A `multipart/form-data` (or urlencoded) request: files plus text fields, with route
/// and query values merged in. Call [`Form::bind`] for the typed, validated fields.
pub struct Form {
    values: Map<String, Value>,
    pub files: Vec<UploadedFile>,
}

impl Form {
    /// The text fields (plus route and query values) as a validated request type.
    pub fn bind<T: DeserializeOwned + Validate>(&self) -> ApiResult<T> {
        bind(self.values.clone())
    }

    /// A text field, matched in any case. For a repeated field, the first value.
    pub fn text(&self, name: &str) -> Option<&str> {
        match self.values.iter().find(|(key, _)| key.eq_ignore_ascii_case(name))?.1 {
            Value::String(text) => Some(text),
            Value::Array(items) => items.first().and_then(Value::as_str),
            _ => None,
        }
    }

    /// Every value of a text field that may repeat.
    pub fn texts(&self, name: &str) -> Vec<&str> {
        match self.values.iter().find(|(key, _)| key.eq_ignore_ascii_case(name)).map(|(_, value)| value) {
            Some(Value::String(text)) => vec![text],
            Some(Value::Array(items)) => items.iter().filter_map(Value::as_str).collect(),
            _ => Vec::new(),
        }
    }

    /// The first file sent under `field` (any case).
    pub fn file(&self, field: &str) -> Option<&UploadedFile> {
        self.files.iter().find(|file| file.field.eq_ignore_ascii_case(field))
    }

    pub fn files_named<'a>(&'a self, field: &'a str) -> impl Iterator<Item = &'a UploadedFile> {
        self.files.iter().filter(move |file| file.field.eq_ignore_ascii_case(field))
    }
}

impl<S: Send + Sync> FromRequest<S> for Form {
    type Rejection = ApiError;

    async fn from_request(request: Request, state: &S) -> Result<Self, ApiError> {
        let (mut parts, body) = request.into_parts();
        let mut values = Map::new();
        let mut files = Vec::new();
        let mut seen = Vec::new();

        let content_type = content_type(&parts);
        if content_type == "multipart/form-data" {
            let request = Request::from_parts(parts.clone(), body);
            let mut multipart = Multipart::from_request(request, state)
                .await
                .map_err(|error| ApiError::binding(lenient::SERIALIZER_ERRORS, error.body_text()))?;
            loop {
                let field = match multipart.next_field().await {
                    Ok(Some(field)) => field,
                    Ok(None) => break,
                    Err(error) => return Err(multipart_error(error)),
                };
                let name = field.name().unwrap_or("").to_owned();
                match field.file_name().map(str::to_owned) {
                    Some(file_name) => {
                        let content_type = field.content_type().unwrap_or("").to_owned();
                        let bytes = field.bytes().await.map_err(multipart_error)?;
                        files.push(UploadedFile { field: name, file_name, content_type, bytes });
                    }
                    None => {
                        let text = field.text().await.map_err(multipart_error)?;
                        put_text(&mut values, &mut seen, &name, text);
                    }
                }
            }
        } else if content_type == "application/x-www-form-urlencoded" {
            let bytes = axum::body::to_bytes(body, MAX_BODY_BYTES).await.map_err(|_| ApiError::PayloadTooLarge)?;
            for (key, value) in url::form_urlencoded::parse(&bytes) {
                put_text(&mut values, &mut seen, &key, value.into_owned());
            }
        } else {
            let bytes = axum::body::to_bytes(body, MAX_BODY_BYTES).await.map_err(|_| ApiError::PayloadTooLarge)?;
            if !bytes.is_empty() {
                return Err(ApiError::UnsupportedMediaType);
            }
        }

        route_and_query(&mut parts, state, &mut values).await;
        Ok(Form { values, files })
    }
}

fn multipart_error(error: axum::extract::multipart::MultipartError) -> ApiError {
    if error.status() == axum::http::StatusCode::PAYLOAD_TOO_LARGE {
        ApiError::PayloadTooLarge
    } else {
        ApiError::binding(lenient::SERIALIZER_ERRORS, error.body_text())
    }
}

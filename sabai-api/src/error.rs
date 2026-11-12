//! API errors and how they become HTTP responses.
//!
//! Shapes match the previous API:
//! - domain errors (`NotFound`, `Conflict`, …) are `application/json`
//!   `{"title","status","detail","instance"}`;
//! - binding and validation failures are `application/problem+json` with an `errors` list;
//! - a missing session is a bare `401`, a missing role a bare `403`.

use axum::http::{HeaderValue, StatusCode, header};
use axum::response::{IntoResponse, Response};
use serde::Serialize;

pub type ApiResult<T> = Result<T, ApiError>;

#[derive(Debug, Clone, Serialize, PartialEq, Eq)]
pub struct FieldError {
    pub name: String,
    pub reason: String,
}

impl FieldError {
    pub fn new(name: impl Into<String>, reason: impl Into<String>) -> Self {
        FieldError { name: name.into(), reason: reason.into() }
    }
}

#[derive(Debug, thiserror::Error)]
pub enum ApiError {
    #[error("{0}")]
    NotFound(String),
    #[error("{0}")]
    Conflict(String),
    #[error("{0}")]
    BadRequest(String),
    #[error("{0}")]
    Forbidden(String),
    /// Wrong credentials: 401 with "Invalid credentials.".
    #[error("Invalid credentials.")]
    AuthRejected,
    /// The record can no longer be edited: 405 titled "Locked".
    #[error("{0}")]
    Locked(String),
    /// The request could not be read (`detail` set) or failed validation.
    #[error("request validation failed")]
    Validation { detail: Option<String>, errors: Vec<FieldError> },
    /// The request was read but its content cannot be used (422), reported per field
    /// like a validation failure.
    #[error("request content is unusable")]
    Unprocessable { errors: Vec<FieldError> },
    /// No session: empty 401.
    #[error("not signed in")]
    Unauthenticated,
    /// Signed in without a required role: empty 403.
    #[error("missing role")]
    AccessDenied,
    #[error("unsupported media type")]
    UnsupportedMediaType,
    #[error("payload too large")]
    PayloadTooLarge,
    #[error("{0}")]
    ServiceUnavailable(String),
    #[error(transparent)]
    Database(sqlx::Error),
    #[error(transparent)]
    Internal(#[from] anyhow::Error),
}

impl ApiError {
    pub fn not_found(message: impl Into<String>) -> Self {
        ApiError::NotFound(message.into())
    }

    pub fn conflict(message: impl Into<String>) -> Self {
        ApiError::Conflict(message.into())
    }

    pub fn bad_request(message: impl Into<String>) -> Self {
        ApiError::BadRequest(message.into())
    }

    pub fn forbidden(message: impl Into<String>) -> Self {
        ApiError::Forbidden(message.into())
    }

    pub fn locked(message: impl Into<String>) -> Self {
        ApiError::Locked(message.into())
    }

    pub fn internal(message: impl std::fmt::Display) -> Self {
        ApiError::Internal(anyhow::anyhow!("{message}"))
    }

    /// A request that could not be bound: one error naming the field (or `serializerErrors`).
    pub fn binding(name: impl Into<String>, reason: impl Into<String>) -> Self {
        let reason = reason.into();
        ApiError::Validation {
            detail: Some(reason.clone()),
            errors: vec![FieldError::new(name, reason)],
        }
    }

    pub fn validation(name: impl Into<String>, reason: impl Into<String>) -> Self {
        ApiError::Validation { detail: None, errors: vec![FieldError::new(name, reason)] }
    }
}

impl From<sqlx::Error> for ApiError {
    fn from(error: sqlx::Error) -> Self {
        ApiError::Database(error)
    }
}

/// A problem body waiting for the request path. `http::context` fills in `instance`
/// (and `traceId`) on the way out, since the error itself does not know the request.
#[derive(Debug, Clone)]
pub struct Problem {
    pub status: StatusCode,
    pub title: &'static str,
    pub kind: ProblemKind,
}

#[derive(Debug, Clone)]
pub enum ProblemKind {
    /// `{"title","status","detail","instance"}` as `application/json`.
    Domain { detail: String },
    /// RFC 7807 with `errors`, as `application/problem+json`.
    Validation { detail: Option<String>, errors: Vec<FieldError> },
    /// Unhandled failure: generic RFC 7807 body, nothing about the cause.
    Unhandled,
}

impl Problem {
    pub fn render(&self, instance: &str, trace_id: &str) -> Response {
        let status = self.status.as_u16();
        let (content_type, body) = match &self.kind {
            ProblemKind::Domain { detail } => {
                // A struct, not `json!`, to keep the properties in this order.
                #[derive(Serialize)]
                struct Body<'a> {
                    title: &'static str,
                    status: u16,
                    detail: &'a str,
                    instance: &'a str,
                }
                (
                    "application/json; charset=utf-8",
                    serde_json::to_string(&Body { title: self.title, status, detail, instance })
                        .unwrap_or_default(),
                )
            }
            ProblemKind::Validation { detail, errors } => {
                #[derive(Serialize)]
                #[serde(rename_all = "camelCase")]
                struct Body<'a> {
                    r#type: &'static str,
                    title: &'static str,
                    status: u16,
                    instance: &'a str,
                    trace_id: &'a str,
                    #[serde(skip_serializing_if = "Option::is_none")]
                    detail: &'a Option<String>,
                    errors: &'a [FieldError],
                }
                (
                    "application/problem+json; charset=utf-8",
                    serde_json::to_string(&Body {
                        r#type: if self.status == StatusCode::UNPROCESSABLE_ENTITY {
                            "https://www.rfc-editor.org/rfc/rfc4918#section-11.2"
                        } else {
                            "https://www.rfc-editor.org/rfc/rfc7231#section-6.5.1"
                        },
                        title: self.title,
                        status,
                        instance,
                        trace_id,
                        detail,
                        errors,
                    })
                    .unwrap_or_default(),
                )
            }
            ProblemKind::Unhandled => (
                "application/problem+json; charset=utf-8",
                format!(
                    r#"{{"type":"https://tools.ietf.org/html/rfc9110#section-15.6.1","title":"{}","status":{status},"traceId":"{trace_id}"}}"#,
                    self.title
                ),
            ),
        };
        let mut response = (self.status, body).into_response();
        response
            .headers_mut()
            .insert(header::CONTENT_TYPE, HeaderValue::from_static(content_type));
        response
    }
}

impl IntoResponse for ApiError {
    fn into_response(self) -> Response {
        let domain = |status: StatusCode, title: &'static str, detail: String| Problem {
            status,
            title,
            kind: ProblemKind::Domain { detail },
        };
        let problem = match self {
            ApiError::NotFound(detail) => domain(StatusCode::NOT_FOUND, "Not Found", detail),
            ApiError::Conflict(detail) => domain(StatusCode::CONFLICT, "Conflict", detail),
            ApiError::BadRequest(detail) => domain(StatusCode::BAD_REQUEST, "Bad Request", detail),
            ApiError::Forbidden(detail) => domain(StatusCode::FORBIDDEN, "Forbidden", detail),
            ApiError::AuthRejected => {
                domain(StatusCode::UNAUTHORIZED, "Unauthorized", "Invalid credentials.".to_owned())
            }
            ApiError::Locked(detail) => domain(StatusCode::METHOD_NOT_ALLOWED, "Locked", detail),
            ApiError::ServiceUnavailable(detail) => {
                domain(StatusCode::SERVICE_UNAVAILABLE, "Service Unavailable", detail)
            }
            ApiError::Validation { detail, errors } => Problem {
                status: StatusCode::BAD_REQUEST,
                title: "Bad Request",
                kind: ProblemKind::Validation { detail, errors },
            },
            ApiError::Unprocessable { errors } => Problem {
                status: StatusCode::UNPROCESSABLE_ENTITY,
                title: "Unprocessable Entity",
                // A lone failure is repeated as the problem's detail.
                kind: ProblemKind::Validation {
                    detail: (errors.len() == 1).then(|| errors[0].reason.clone()),
                    errors,
                },
            },
            ApiError::Unauthenticated => return StatusCode::UNAUTHORIZED.into_response(),
            ApiError::AccessDenied => return StatusCode::FORBIDDEN.into_response(),
            ApiError::UnsupportedMediaType => {
                return StatusCode::UNSUPPORTED_MEDIA_TYPE.into_response();
            }
            ApiError::PayloadTooLarge => return StatusCode::PAYLOAD_TOO_LARGE.into_response(),
            ApiError::Database(error) => match database_conflict(&error) {
                Some(detail) => {
                    tracing::warn!(%error, "Handled API exception: Conflict");
                    domain(StatusCode::CONFLICT, "Conflict", detail.to_owned())
                }
                None => {
                    tracing::error!(%error, "Unhandled API exception");
                    unhandled()
                }
            },
            ApiError::Internal(error) => {
                tracing::error!(error = format!("{error:#}"), "Unhandled API exception");
                unhandled()
            }
        };
        if !matches!(problem.kind, ProblemKind::Unhandled) && problem.status != StatusCode::CONFLICT {
            tracing::debug!(status = %problem.status, title = problem.title, "Handled API exception");
        }
        let mut response = problem.status.into_response();
        response.extensions_mut().insert(problem);
        response
    }
}

fn unhandled() -> Problem {
    Problem {
        status: StatusCode::INTERNAL_SERVER_ERROR,
        title: "An error occurred while processing your request.",
        kind: ProblemKind::Unhandled,
    }
}

/// Unique and foreign-key violations are the caller's doing: 409, not 500.
fn database_conflict(error: &sqlx::Error) -> Option<&'static str> {
    let code = error.as_database_error()?.code()?;
    match code.as_ref() {
        "23505" => Some("A record with the same unique value already exists."),
        "23503" => Some("The record is still referenced or a related record is missing."),
        _ => None,
    }
}

/// `Option` → `NotFound`, the `FirstOrNotFoundAsync` of this codebase.
pub trait OrNotFound<T> {
    fn or_not_found(self, message: &str) -> ApiResult<T>;
}

impl<T> OrNotFound<T> for Option<T> {
    fn or_not_found(self, message: &str) -> ApiResult<T> {
        self.ok_or_else(|| ApiError::NotFound(message.to_owned()))
    }
}

impl<T> OrNotFound<T> for Result<Option<T>, sqlx::Error> {
    fn or_not_found(self, message: &str) -> ApiResult<T> {
        self?.ok_or_else(|| ApiError::NotFound(message.to_owned()))
    }
}

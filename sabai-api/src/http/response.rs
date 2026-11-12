//! Response helpers.

use axum::http::{HeaderValue, StatusCode, header};
use axum::response::{IntoResponse, IntoResponseParts, Response, ResponseParts};
use serde::Serialize;

/// A JSON body (`application/json; charset=utf-8`, camelCase is up to the type).
pub struct Json<T>(pub T);

impl<T: Serialize> IntoResponse for Json<T> {
    fn into_response(self) -> Response {
        match serde_json::to_vec(&self.0) {
            Ok(body) => (
                [(header::CONTENT_TYPE, HeaderValue::from_static("application/json; charset=utf-8"))],
                body,
            )
                .into_response(),
            Err(error) => crate::error::ApiError::internal(error).into_response(),
        }
    }
}

/// `201 Created` with a `Location` header and a JSON body.
pub struct Created<T> {
    pub location: String,
    pub body: T,
}

impl<T> Created<T> {
    pub fn at(location: impl Into<String>, body: T) -> Self {
        Created { location: location.into(), body }
    }
}

impl<T: Serialize> IntoResponse for Created<T> {
    fn into_response(self) -> Response {
        let mut response = (StatusCode::CREATED, Json(self.body)).into_response();
        if let Ok(location) = HeaderValue::from_str(&self.location) {
            response.headers_mut().insert(header::LOCATION, location);
        }
        response
    }
}

/// `204 No Content`.
pub struct NoContent;

impl IntoResponse for NoContent {
    fn into_response(self) -> Response {
        StatusCode::NO_CONTENT.into_response()
    }
}

/// `Set-Cookie` headers to add to a response: `(SetCookies(cookies), Json(body))`.
pub struct SetCookies(pub Vec<HeaderValue>);

impl IntoResponseParts for SetCookies {
    type Error = std::convert::Infallible;

    fn into_response_parts(self, mut parts: ResponseParts) -> Result<ResponseParts, Self::Error> {
        for cookie in self.0 {
            parts.headers_mut().append(header::SET_COOKIE, cookie);
        }
        Ok(parts)
    }
}

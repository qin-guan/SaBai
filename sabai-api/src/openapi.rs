//! API description.
//!
//! `openapi/v1.json` is the contract: the document the .NET API generated, kept as a
//! file and served as-is, so the TypeScript SDK generated from it stays valid. Change it
//! together with the endpoint it describes; `tests/api/contract.rs` fails when a
//! documented operation has no route.

use axum::Router;
use axum::http::header;
use axum::response::{Html, IntoResponse};
use axum::routing::get;

use crate::state::AppState;

pub const DOCUMENT: &str = include_str!("../openapi/v1.json");

const REFERENCE_PAGE: &str = r#"<!doctype html>
<html>
  <head>
    <title>Sa'Bai API</title>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
  </head>
  <body>
    <script id="api-reference" data-url="/openapi/v1.json"></script>
    <script src="https://cdn.jsdelivr.net/npm/@scalar/api-reference"></script>
  </body>
</html>
"#;

async fn document() -> impl IntoResponse {
    ([(header::CONTENT_TYPE, "application/json; charset=utf-8")], DOCUMENT)
}

async fn reference() -> Html<&'static str> {
    Html(REFERENCE_PAGE)
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/openapi/v1.json", get(document))
        .route("/scalar", get(reference))
        .route("/scalar/v1", get(reference))
}

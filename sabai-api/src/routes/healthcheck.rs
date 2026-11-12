use axum::Router;
use axum::routing::get;

use crate::http::Json;
use crate::state::AppState;

/// Public liveness string.
async fn healthcheck() -> Json<&'static str> {
    Json("yay!")
}

pub fn router() -> Router<AppState> {
    Router::new().route("/healthcheck", get(healthcheck))
}

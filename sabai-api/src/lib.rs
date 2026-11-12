//! Sa'Bai clinic API.
//!
//! - [`routes`]: the HTTP endpoints, one module per area.
//! - [`domain`]: the tables and the rules that belong to the data.
//! - [`db`]: audited transactions and migrations.
//! - [`auth`]: staff accounts and cookie sessions.
//! - [`http`]: request binding, validation and the per-request context.
//! - [`face`], [`storage`], [`pdf`]: face recognition, the file bucket, consult reports.

pub mod auth;
pub mod config;
pub mod db;
pub mod domain;
pub mod error;
pub mod face;
pub mod http;
pub mod openapi;
pub mod pdf;
pub mod routes;
pub mod state;
pub mod storage;
pub mod time;

use axum::Router;
use axum::extract::DefaultBodyLimit;
use axum::http::HeaderValue;
use tower_http::cors::{AllowHeaders, AllowMethods, AllowOrigin, CorsLayer};
use tower_http::trace::TraceLayer;

use crate::config::Settings;
use crate::state::{AppState, Inner};

/// Connects to the database, migrates it, seeds roles and configured accounts, and
/// assembles the application state.
pub async fn build_state(settings: Settings) -> anyhow::Result<AppState> {
    let db = db::connect(&settings.database).await?;
    db::migrate::run(&db).await?;
    auth::seed::ensure(&db, &settings.auth, &settings.identity).await?;
    let keys = auth::session::SessionKeys::load(&db).await?;
    let storage = storage::Storage::new(&settings.storage)?;
    let faces = face::FaceService { settings: settings.face.clone() };
    Ok(AppState::new(Inner { settings, db, keys, storage, faces }))
}

/// The HTTP application.
pub fn app(state: AppState) -> Router {
    // The web apps proxy /api to this service, so browsers normally never make
    // cross-origin calls. The policy remains for tools that call the API directly from
    // another origin; credentials are allowed so the session cookie is sent.
    let origins: Vec<HeaderValue> =
        state.settings.cors_allowed_origins.iter().filter_map(|origin| origin.parse().ok()).collect();
    let cors = CorsLayer::new()
        .allow_origin(AllowOrigin::list(origins))
        .allow_headers(AllowHeaders::mirror_request())
        .allow_methods(AllowMethods::mirror_request())
        .allow_credentials(true);

    let mut router = routes::router().merge(openapi::router());
    if !state.settings.is_production() {
        router = router.merge(routes::health_router());
    }
    router
        .layer(DefaultBodyLimit::max(http::request::MAX_BODY_BYTES))
        .layer(axum::middleware::from_fn_with_state(state.clone(), http::context::layer))
        .layer(cors)
        .layer(TraceLayer::new_for_http())
        .with_state(state)
}

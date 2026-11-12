//! HTTP endpoints, one module per area of the clinic.

pub mod audit_logs;
pub mod auth;
pub mod broadcasts;
pub mod child_vitals;
pub mod consults;
pub mod dental;
pub mod diagnoses;
pub mod eyesight;
pub mod faces;
pub mod files;
pub mod glasses;
pub mod healthcheck;
pub mod medication_brands;
pub mod medication_history;
pub mod medication_ingredients;
pub mod medication_stock;
pub mod orders;
pub mod patient_consult;
pub mod patient_faces;
pub mod patient_records;
pub mod patients;
pub mod persons;
pub mod pharmacy_orders;
pub mod puberty;
pub mod referrals;
pub mod village_codes;
pub mod vision;
pub mod visits;
pub mod vitals;

use axum::Router;
use axum::routing::get;

use crate::state::AppState;

async fn healthy() -> &'static str {
    "Healthy"
}

/// Readiness and liveness probes. Not exposed in production, as before.
pub fn health_router() -> Router<AppState> {
    Router::new().route("/health", get(healthy)).route("/alive", get(healthy))
}

pub fn router() -> Router<AppState> {
    Router::new()
        .merge(audit_logs::router())
        .merge(auth::router())
        .merge(broadcasts::router())
        .merge(child_vitals::router())
        .merge(consults::router())
        .merge(dental::router())
        .merge(diagnoses::router())
        .merge(eyesight::router())
        .merge(faces::router())
        .merge(files::router())
        .merge(glasses::router())
        .merge(healthcheck::router())
        .merge(medication_brands::router())
        .merge(medication_history::router())
        .merge(medication_ingredients::router())
        .merge(medication_stock::router())
        .merge(orders::router())
        .merge(patient_consult::router())
        .merge(patient_faces::router())
        .merge(patient_records::router())
        .merge(patients::router())
        .merge(persons::router())
        .merge(pharmacy_orders::router())
        .merge(puberty::router())
        .merge(referrals::router())
        .merge(village_codes::router())
        .merge(vision::router())
        .merge(visits::router())
        .merge(vitals::router())
}

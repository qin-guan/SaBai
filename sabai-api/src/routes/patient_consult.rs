//! The consult workspace for a visit: the patient chart with consults in the order they were written.

use axum::Router;
use axum::routing::get;

use super::patient_records::{ChartRequest, ChartResponse, ConsultOrder, chart};
use crate::auth::Staff;
use crate::error::ApiResult;
use crate::http::{Ctx, Json, Req};
use crate::state::AppState;

/// Load the consult workspace for a visit. Consults are ordered by date, then id.
async fn patient_consult(ctx: Ctx, _: Staff, Req(req): Req<ChartRequest>) -> ApiResult<Json<ChartResponse>> {
    Ok(Json(chart(&ctx, req.visit_id, ConsultOrder::OldestFirst).await?))
}

pub fn router() -> Router<AppState> {
    Router::new().route("/patient-consult", get(patient_consult))
}

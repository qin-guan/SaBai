//! Medication history: what left and entered the shelves for one brand.

use axum::Router;
use axum::routing::get;
use serde::{Deserialize, Serialize};

use crate::auth::Staff;
use crate::domain::{OrderStatus, StockChangeField, StockStatus};
use crate::error::{ApiError, ApiResult};
use crate::http::{Ctx, Json, Req, Validate};
use crate::state::AppState;
use crate::time::Ts;

/// Brand whose approved dispenses and quantity adjustments should be listed.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct HistoryRequest {
    pub brand_id: Option<i32>,
}

impl Validate for HistoryRequest {}

/// An approved dispense or a catalog quantity adjustment, newest first.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct HistoryResponse {
    pub kind: &'static str,
    pub actor_name: String,
    pub patient_name: String,
    pub quantity_changed: i32,
    pub quantity_remaining: i32,
    pub date: Ts,
}

fn name_or_dash(name: Option<String>) -> String {
    name.filter(|name| !name.trim().is_empty()).unwrap_or_else(|| "-".to_owned())
}

/// The quantity recorded in a stock change, or zero when it is not a number.
fn parse_change(value: &str) -> i32 {
    value.trim().parse().unwrap_or(0)
}

/// List approved dispenses and stock adjustments for a brand.
async fn history(ctx: Ctx, _: Staff, Req(req): Req<HistoryRequest>) -> ApiResult<Json<Vec<HistoryResponse>>> {
    let Some(brand_id) = req.brand_id.filter(|id| *id > 0) else {
        return Err(ApiError::bad_request("brandId is required."));
    };

    let quantity_remaining: i32 = sqlx::query_scalar(
        r#"SELECT COALESCE(SUM("Quantity"), 0)::int4 FROM "MedicationStocks"
           WHERE "MedicationBrandId" = $1 AND "StockStatus" = $2"#,
    )
    .bind(brand_id)
    .bind(StockStatus::Active)
    .fetch_one(ctx.db())
    .await?;

    let orders: Vec<(Option<String>, Option<String>, i32, Ts)> = sqlx::query_as(
        r#"SELECT u."Nickname", p."Name", o."Quantity", COALESCE(o."ApprovedAt", o."CreatedAt")
           FROM "Orders" o
           JOIN "Consults" c ON c."Id" = o."ConsultId"
           JOIN "Visits" v ON v."Id" = c."VisitId"
           JOIN "Patients" p ON p."Id" = v."PatientId"
           LEFT JOIN "StaffUsers" u ON u."Id" = o."ApprovedById"
           WHERE o."MedicationBrandId" = $1 AND o."Status" = $2
           ORDER BY o."Id""#,
    )
    .bind(brand_id)
    .bind(OrderStatus::Approved)
    .fetch_all(ctx.db())
    .await?;

    let changes: Vec<(String, String, Option<String>, Ts)> = sqlx::query_as(
        r#"SELECT sc."PreviousValue", sc."NewValue", u."Nickname", sc."CreatedAt"
           FROM "StockChanges" sc
           JOIN "MedicationStocks" s ON s."Id" = sc."StockId"
           JOIN "StaffUsers" u ON u."Id" = sc."UserId"
           WHERE s."MedicationBrandId" = $1 AND sc."Field" = $2
           ORDER BY sc."Id""#,
    )
    .bind(brand_id)
    .bind(StockChangeField::Quantity)
    .fetch_all(ctx.db())
    .await?;

    let mut history: Vec<HistoryResponse> = orders
        .into_iter()
        .map(|(actor, patient, quantity, date)| HistoryResponse {
            kind: "order",
            actor_name: name_or_dash(actor),
            patient_name: name_or_dash(patient),
            quantity_changed: -quantity,
            quantity_remaining,
            date,
        })
        .chain(changes.into_iter().map(|(previous, new, actor, date)| HistoryResponse {
            kind: "adjustment",
            actor_name: name_or_dash(actor),
            patient_name: "-".to_owned(),
            quantity_changed: parse_change(&new).wrapping_sub(parse_change(&previous)),
            quantity_remaining,
            date,
        }))
        .collect();
    history.sort_by(|a, b| b.date.cmp(&a.date).then_with(|| a.kind.cmp(b.kind)));
    Ok(Json(history))
}

pub fn router() -> Router<AppState> {
    Router::new().route("/medication-history", get(history))
}

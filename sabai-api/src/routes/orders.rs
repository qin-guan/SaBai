//! Medication orders: what a consult prescribed, and the pharmacy's decision on each.
//!
//! Approving an order takes its quantity from the brand's active lots, earliest expiry
//! first, and records each deduction in the stock change log, linked to the order.

use axum::Router;
use axum::routing::get;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use crate::auth::Staff;
use crate::db::Tx;
use crate::domain::{MedicationStock, Order, OrderItem, OrderStatus, StockChange, StockChangeField, StockStatus};
use crate::error::{ApiError, ApiResult, OrNotFound};
use crate::http::{Created, Ctx, Json, NoContent, Req, Rules, Validate};
use crate::state::AppState;
use crate::time::Ts;

const NOT_FOUND: &str = "Order was not found.";

/// Medication order with its brand name.
#[derive(Debug, Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
pub struct OrderResponse {
    #[sqlx(rename = "Id")]
    pub id: i32,
    #[sqlx(rename = "ConsultId")]
    pub consult_id: i32,
    #[sqlx(rename = "MedicationBrandId")]
    pub medication_brand_id: i32,
    #[sqlx(rename = "BrandName")]
    pub brand_name: String,
    #[sqlx(rename = "Quantity")]
    pub quantity: i32,
    #[sqlx(rename = "Status")]
    pub status: OrderStatus,
    #[sqlx(rename = "DosageInstructions")]
    pub dosage_instructions: String,
    #[sqlx(rename = "Remarks")]
    pub remarks: Option<String>,
    #[sqlx(rename = "ApprovedById")]
    pub approved_by_id: Option<Uuid>,
    #[sqlx(rename = "ApprovedAt")]
    pub approved_at: Option<Ts>,
    #[sqlx(rename = "CreatedAt")]
    pub created_at: Ts,
}

impl OrderResponse {
    fn from(order: Order, brand_name: String) -> Self {
        OrderResponse {
            id: order.id,
            consult_id: order.consult_id,
            medication_brand_id: order.medication_brand_id,
            brand_name,
            quantity: order.quantity,
            status: order.status,
            dosage_instructions: order.dosage_instructions,
            remarks: order.remarks,
            approved_by_id: order.approved_by_id,
            approved_at: order.approved_at,
            created_at: order.created_at,
        }
    }
}

const SELECT: &str = r#"SELECT o."Id", o."ConsultId", o."MedicationBrandId", b."Name" AS "BrandName", o."Quantity",
       o."Status", o."DosageInstructions", o."Remarks", o."ApprovedById", o."ApprovedAt", o."CreatedAt"
FROM "Orders" o
JOIN "MedicationBrands" b ON b."Id" = o."MedicationBrandId""#;

/// Trimmed text, or nothing when it is absent or blank.
fn non_blank(text: Option<&str>) -> Option<String> {
    text.map(str::trim).filter(|text| !text.is_empty()).map(str::to_owned)
}

async fn read(conn: &mut sqlx::PgConnection, id: i32) -> ApiResult<OrderResponse> {
    sqlx::query_as(&format!(r#"{SELECT} WHERE o."Id" = $1"#)).bind(id).fetch_optional(conn).await.or_not_found(NOT_FOUND)
}

/// Optional order status filter. Accepts PENDING, APPROVED, or CANCELLED.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ListRequest {
    pub status: Option<String>,
}

impl Validate for ListRequest {}

/// List medication orders.
async fn list(ctx: Ctx, _: Staff, Req(req): Req<ListRequest>) -> ApiResult<Json<Vec<OrderResponse>>> {
    let status = match req.status.as_deref().map(str::trim).filter(|status| !status.is_empty()) {
        Some(text) => {
            let wanted = text.to_uppercase();
            let status = OrderStatus::ALL
                .iter()
                .copied()
                .find(|status| status.wire() == wanted)
                .ok_or_else(|| ApiError::bad_request("status must be PENDING, APPROVED, or CANCELLED."))?;
            Some(status)
        }
        None => None,
    };
    let orders = sqlx::query_as(&format!(r#"{SELECT} WHERE ($1::text IS NULL OR o."Status" = $1) ORDER BY o."Id" DESC"#))
        .bind(status)
        .fetch_all(ctx.db())
        .await?;
    Ok(Json(orders))
}

/// Identifies a medication order.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct IdRequest {
    #[serde(default)]
    pub id: i32,
}

impl Validate for IdRequest {}

/// Get a medication order.
async fn get_one(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<Json<OrderResponse>> {
    let mut conn = ctx.db().acquire().await?;
    Ok(Json(read(&mut conn, req.id).await?))
}

/// Pending medication order. Creating an order does not change stock.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateRequest {
    #[serde(default)]
    pub consult_id: i32,
    #[serde(default)]
    pub medication_brand_id: i32,
    #[serde(default)]
    pub quantity: i32,
    pub dosage_instructions: String,
    pub remarks: Option<String>,
}

impl Validate for CreateRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("consultId", self.consult_id, 0);
        v.greater_than("medicationBrandId", self.medication_brand_id, 0);
        v.greater_than("quantity", self.quantity, 0);
        v.not_empty("dosageInstructions", &self.dosage_instructions);
    }
}

/// Create a pending medication order.
async fn create(ctx: Ctx, _: Staff, Req(req): Req<CreateRequest>) -> ApiResult<Created<OrderResponse>> {
    let mut tx = ctx.begin().await?;
    let consult_exists: bool = sqlx::query_scalar(r#"SELECT EXISTS (SELECT 1 FROM "Consults" WHERE "Id" = $1)"#)
        .bind(req.consult_id)
        .fetch_one(&mut *tx)
        .await?;
    if !consult_exists {
        return Err(ApiError::not_found("Consult was not found."));
    }
    let brand_name: String = sqlx::query_scalar(r#"SELECT "Name" FROM "MedicationBrands" WHERE "Id" = $1"#)
        .bind(req.medication_brand_id)
        .fetch_optional(&mut *tx)
        .await
        .or_not_found("Medication brand was not found.")?;

    let order = tx
        .insert(Order {
            id: 0,
            consult_id: req.consult_id,
            medication_brand_id: req.medication_brand_id,
            dosage_instructions: req.dosage_instructions.trim().to_owned(),
            remarks: non_blank(req.remarks.as_deref()),
            status: OrderStatus::Pending,
            quantity: req.quantity,
            approved_by_id: None,
            approved_at: None,
            created_at: Ts::now(),
        })
        .await?;
    tx.commit().await?;
    Ok(Created::at(format!("/orders/{}", order.id), OrderResponse::from(order, brand_name)))
}

/// Status transition for a pending order. Notes may be updated with the status.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateRequest {
    #[serde(default)]
    pub id: i32,
    pub status: Option<OrderStatus>,
    pub dosage_instructions: Option<String>,
    pub remarks: Option<String>,
}

impl Validate for UpdateRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("id", self.id, 0);
        v.not_null("status", &self.status);
        if self.dosage_instructions.is_some() {
            v.not_empty("dosageInstructions", &self.dosage_instructions);
        }
    }
}

/// Update or approve a medication order.
async fn update(ctx: Ctx, Staff(user): Staff, Req(req): Req<UpdateRequest>) -> ApiResult<Json<OrderResponse>> {
    let mut tx = ctx.begin().await?;
    // The order's row is locked for the rest of the transaction: of two requests deciding
    // the same order, the second waits here and then sees it is no longer pending.
    let before: Order = sqlx::query_as(r#"SELECT * FROM "Orders" WHERE "Id" = $1 FOR UPDATE"#)
        .bind(req.id)
        .fetch_optional(&mut *tx)
        .await
        .or_not_found(NOT_FOUND)?;
    if matches!(before.status, OrderStatus::Approved | OrderStatus::Cancelled) {
        return Err(ApiError::bad_request(format!("Order is already {}.", before.status.name().to_uppercase())));
    }

    let mut order = before.clone();
    if let Some(dosage_instructions) = &req.dosage_instructions {
        order.dosage_instructions = dosage_instructions.trim().to_owned();
    }
    if let Some(remarks) = &req.remarks {
        order.remarks = non_blank(Some(remarks));
    }

    match req.status {
        Some(OrderStatus::Approved) => {
            let noted = tx.update(&before, order).await?;
            let now = Ts::now();
            let changes = dispense(&mut tx, &noted, user.id, now).await?;
            for change in &changes {
                tx.insert(OrderItem { order_id: noted.id, stock_change_id: change.id }).await?;
            }
            let mut approved = noted.clone();
            approved.status = OrderStatus::Approved;
            approved.approved_by_id = Some(user.id);
            approved.approved_at = Some(now);
            tx.update(&noted, approved).await?;
        }
        status => {
            if status == Some(OrderStatus::Cancelled) {
                order.status = OrderStatus::Cancelled;
            }
            tx.update(&before, order).await?;
        }
    }

    let response = read(&mut tx, req.id).await?;
    tx.commit().await?;
    Ok(Json(response))
}

/// Takes the order's quantity from the brand's active lots, earliest expiry first, and
/// returns the stock change written for each lot touched. A lot that is emptied becomes
/// dispensed.
///
/// The lots are locked while they are read, so approvals of the same brand that run at
/// the same time take from the stock one after the other and can never hand out more
/// than there is.
async fn dispense(tx: &mut Tx, order: &Order, user_id: Uuid, now: Ts) -> ApiResult<Vec<StockChange>> {
    let lots: Vec<MedicationStock> = sqlx::query_as(
        r#"SELECT * FROM "MedicationStocks"
           WHERE "MedicationBrandId" = $1 AND "StockStatus" = $2 AND "Quantity" > 0
           ORDER BY "Expiry", "Id"
           FOR UPDATE"#,
    )
    .bind(order.medication_brand_id)
    .bind(StockStatus::Active)
    .fetch_all(&mut **tx)
    .await?;
    if lots.iter().map(|lot| i64::from(lot.quantity)).sum::<i64>() < i64::from(order.quantity) {
        return Err(ApiError::bad_request("Medicine stock cannot be negative."));
    }

    let mut remaining = order.quantity;
    let mut pending = Vec::new();
    for lot in lots {
        if remaining == 0 {
            break;
        }
        let take = lot.quantity.min(remaining);
        remaining -= take;
        let mut taken = lot.clone();
        taken.quantity -= take;
        if taken.quantity == 0 {
            taken.stock_status = StockStatus::Dispensed;
        }
        let taken = tx.update(&lot, taken).await?;
        pending.push(StockChange {
            id: 0,
            stock_id: taken.id,
            field: StockChangeField::Quantity,
            previous_value: lot.quantity.to_string(),
            new_value: taken.quantity.to_string(),
            user_id,
            created_at: now,
        });
    }
    let mut changes = Vec::with_capacity(pending.len());
    for change in pending {
        changes.push(tx.insert(change).await?);
    }
    Ok(changes)
}

/// Delete a medication order without reversing stock.
async fn delete(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<NoContent> {
    let mut tx = ctx.begin().await?;
    let order: Order = sqlx::query_as(r#"SELECT * FROM "Orders" WHERE "Id" = $1"#)
        .bind(req.id)
        .fetch_optional(&mut *tx)
        .await
        .or_not_found(NOT_FOUND)?;
    tx.delete(&order).await?;
    tx.commit().await?;
    Ok(NoContent)
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/orders", get(list).post(create))
        .route("/orders/{id}", get(get_one).patch(update).delete(delete))
}

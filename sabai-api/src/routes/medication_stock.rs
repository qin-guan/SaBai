//! Medication stock: the lots on the shelves, and the adjustments and splits made to them.
//!
//! Every change to a lot's quantity, location, status or remarks is also written to the
//! stock change log, in the same transaction.

use std::collections::{HashMap, HashSet};

use axum::Router;
use axum::routing::{get, patch, post};
use serde::{Deserialize, Serialize};

use crate::auth::Staff;
use crate::domain::stock_change_log::{self, Snapshot};
use crate::domain::{MedicationStock, StockStatus};
use crate::error::{ApiError, ApiResult, OrNotFound};
use crate::http::{Created, Ctx, Json, Req, Rules, Validate};
use crate::state::AppState;
use crate::time::Ts;

const NOT_FOUND: &str = "Medication stock was not found.";

/// A medication stock lot.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct StockResponse {
    pub id: i32,
    pub medication_brand_id: i32,
    pub quantity: i32,
    pub expiry: Ts,
    pub location: String,
    pub stock_status: StockStatus,
    pub remarks: Option<String>,
}

impl From<MedicationStock> for StockResponse {
    fn from(stock: MedicationStock) -> Self {
        StockResponse {
            id: stock.id,
            medication_brand_id: stock.medication_brand_id,
            quantity: stock.quantity,
            expiry: stock.expiry,
            location: stock.location,
            stock_status: stock.stock_status,
            remarks: stock.remarks,
        }
    }
}

/// Trimmed text, or nothing when it is absent or blank.
fn non_blank(text: Option<&str>) -> Option<String> {
    text.map(str::trim).filter(|text| !text.is_empty()).map(str::to_owned)
}

fn active() -> StockStatus {
    StockStatus::Active
}

/// Loads the lot and locks its row until the transaction ends, so concurrent edits and
/// order approvals of the same lot apply one after the other.
async fn find_for_update(conn: &mut sqlx::PgConnection, id: i32) -> ApiResult<MedicationStock> {
    sqlx::query_as(r#"SELECT * FROM "MedicationStocks" WHERE "Id" = $1 FOR UPDATE"#)
        .bind(id)
        .fetch_optional(conn)
        .await
        .or_not_found(NOT_FOUND)
}

/// List medication stock excluding empty lots.
async fn list(ctx: Ctx, _: Staff) -> ApiResult<Json<Vec<StockResponse>>> {
    let stocks: Vec<MedicationStock> =
        sqlx::query_as(r#"SELECT * FROM "MedicationStocks" WHERE "Quantity" <> 0 ORDER BY "Id""#)
            .fetch_all(ctx.db())
            .await?;
    Ok(Json(stocks.into_iter().map(Into::into).collect()))
}

/// Non-empty stock lot with brand and ingredient names. `isLowStock` flags an active lot
/// whose ingredient's total active quantity is at or below the ingredient's `fallBelow`.
#[derive(Debug, Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
pub struct StockWithBrandResponse {
    #[sqlx(rename = "Id")]
    pub id: i32,
    #[sqlx(rename = "MedicationBrandId")]
    pub medication_brand_id: i32,
    #[sqlx(rename = "Quantity")]
    pub quantity: i32,
    #[sqlx(rename = "Expiry")]
    pub expiry: Ts,
    #[sqlx(rename = "Location")]
    pub location: String,
    #[sqlx(rename = "StockStatus")]
    pub stock_status: StockStatus,
    #[sqlx(rename = "Remarks")]
    pub remarks: Option<String>,
    #[sqlx(rename = "BrandName")]
    pub brand_name: String,
    #[sqlx(rename = "IngredientName")]
    pub ingredient_name: String,
    #[sqlx(rename = "IngredientId")]
    pub ingredient_id: i32,
    #[sqlx(rename = "FallBelow")]
    pub fall_below: i32,
    #[sqlx(skip)]
    pub is_low_stock: bool,
}

/// List medication stock with brand and ingredient names.
async fn list_with_brand(ctx: Ctx, _: Staff) -> ApiResult<Json<Vec<StockWithBrandResponse>>> {
    let mut stocks: Vec<StockWithBrandResponse> = sqlx::query_as(
        r#"SELECT s."Id", s."MedicationBrandId", s."Quantity", s."Expiry", s."Location", s."StockStatus",
                  s."Remarks", b."Name" AS "BrandName", i."Name" AS "IngredientName",
                  b."ActiveIngredientId" AS "IngredientId", i."FallBelow"
           FROM "MedicationStocks" s
           JOIN "MedicationBrands" b ON b."Id" = s."MedicationBrandId"
           JOIN "MedicationActiveIngredients" i ON i."Id" = b."ActiveIngredientId"
           WHERE s."Quantity" <> 0
           ORDER BY s."Id" DESC"#,
    )
    .fetch_all(ctx.db())
    .await?;

    let mut active_totals: HashMap<i32, i64> = HashMap::new();
    for stock in stocks.iter().filter(|stock| stock.stock_status == StockStatus::Active) {
        *active_totals.entry(stock.ingredient_id).or_default() += i64::from(stock.quantity);
    }
    for stock in &mut stocks {
        stock.is_low_stock = stock.stock_status == StockStatus::Active
            && active_totals.get(&stock.ingredient_id).copied().unwrap_or(0) <= i64::from(stock.fall_below);
    }
    Ok(Json(stocks))
}

/// New stock lot. Quantity may be zero.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateRequest {
    #[serde(default)]
    pub medication_brand_id: i32,
    #[serde(default)]
    pub quantity: i32,
    #[serde(default)]
    pub expiry: Option<Ts>,
    pub location: String,
    #[serde(default = "active")]
    pub stock_status: StockStatus,
    pub remarks: Option<String>,
}

impl Validate for CreateRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("medicationBrandId", self.medication_brand_id, 0);
        v.greater_than_or_equal("quantity", self.quantity, 0);
        v.must(
            "expiry",
            self.expiry.is_some_and(|expiry| expiry != Ts::min()),
            "'expiry' must not be equal to '01/01/0001 00:00:00 +00:00'.",
        );
        v.not_empty("location", &self.location).max_len("location", &self.location, 255);
    }
}

/// Create a medication stock lot.
async fn create(ctx: Ctx, Staff(user): Staff, Req(req): Req<CreateRequest>) -> ApiResult<Created<StockResponse>> {
    let mut tx = ctx.begin().await?;
    let brand_exists: bool = sqlx::query_scalar(r#"SELECT EXISTS (SELECT 1 FROM "MedicationBrands" WHERE "Id" = $1)"#)
        .bind(req.medication_brand_id)
        .fetch_one(&mut *tx)
        .await?;
    if !brand_exists {
        return Err(ApiError::not_found("Medication brand was not found."));
    }
    let stock = tx
        .insert(MedicationStock {
            id: 0,
            medication_brand_id: req.medication_brand_id,
            quantity: req.quantity,
            expiry: req.expiry.unwrap_or_else(Ts::min),
            location: req.location.trim().to_owned(),
            stock_status: req.stock_status,
            remarks: non_blank(req.remarks.as_deref()),
        })
        .await?;
    for change in stock_change_log::for_create(&stock, user.id, Ts::now()) {
        tx.insert(change).await?;
    }
    tx.commit().await?;
    Ok(Created::at("/medication-stock", stock.into()))
}

/// Location, status, and remarks update. Blank or omitted remarks are cleared.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateRequest {
    #[serde(default)]
    pub id: i32,
    pub location: Option<String>,
    pub stock_status: Option<StockStatus>,
    pub remarks: Option<String>,
}

impl Validate for UpdateRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("id", self.id, 0);
        v.max_len("location", &self.location, 255);
    }
}

/// Update stock location, status, or remarks.
async fn update(ctx: Ctx, Staff(user): Staff, Req(req): Req<UpdateRequest>) -> ApiResult<Json<StockResponse>> {
    let mut tx = ctx.begin().await?;
    let before = find_for_update(&mut tx, req.id).await?;
    let mut stock = before.clone();
    if let Some(location) = non_blank(req.location.as_deref()) {
        stock.location = location;
    }
    if let Some(status) = req.stock_status {
        stock.stock_status = status;
    }
    stock.remarks = non_blank(req.remarks.as_deref());

    let changes = stock_change_log::for_update(&Snapshot::of(&before), &stock, user.id, Ts::now());
    let stock = tx.update(&before, stock).await?;
    for change in changes {
        tx.insert(change).await?;
    }
    tx.commit().await?;
    Ok(Json(stock.into()))
}

/// Catalog quantity adjustment. The change may be negative.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AdjustRequest {
    #[serde(default)]
    pub id: i32,
    #[serde(default)]
    pub quantity_change: i32,
    pub remarks: Option<String>,
}

impl Validate for AdjustRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("id", self.id, 0);
    }
}

/// Adjust a stock lot quantity.
async fn adjust(ctx: Ctx, Staff(user): Staff, Req(req): Req<AdjustRequest>) -> ApiResult<Json<StockResponse>> {
    let mut tx = ctx.begin().await?;
    let before = find_for_update(&mut tx, req.id).await?;
    let new_quantity = i64::from(before.quantity) + i64::from(req.quantity_change);
    let Some(new_quantity) = i32::try_from(new_quantity).ok().filter(|quantity| *quantity >= 0) else {
        return Err(ApiError::bad_request("Stock quantity cannot be negative."));
    };
    let mut stock = before.clone();
    stock.quantity = new_quantity;
    if let Some(remarks) = &req.remarks {
        stock.remarks = non_blank(Some(remarks));
    }

    let changes = stock_change_log::for_update(&Snapshot::of(&before), &stock, user.id, Ts::now());
    let stock = tx.update(&before, stock).await?;
    for change in changes {
        tx.insert(change).await?;
    }
    tx.commit().await?;
    Ok(Json(stock.into()))
}

/// One child lot produced by splitting a parent stock row.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SplitItem {
    #[serde(default)]
    pub quantity: i32,
    pub location: String,
    #[serde(default = "active")]
    pub stock_status: StockStatus,
    pub remarks: Option<String>,
}

/// Split a parent stock lot into two to ten distinct children.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SplitRequest {
    #[serde(default)]
    pub id: i32,
    #[serde(default = "no_splits")]
    pub splits: Option<Vec<SplitItem>>,
}

fn no_splits() -> Option<Vec<SplitItem>> {
    Some(Vec::new())
}

impl Validate for SplitRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("id", self.id, 0);
        let Some(splits) = &self.splits else {
            v.not_null("splits", &self.splits);
            return;
        };
        v.must("splits", (2..=10).contains(&splits.len()), "The specified condition was not met for 'splits'.");
        for (index, split) in splits.iter().enumerate() {
            v.greater_than(&format!("splits[{index}].quantity"), split.quantity, 0);
            let location = format!("splits[{index}].location");
            v.not_empty(&location, &split.location).max_len(&location, &split.location, 255);
        }
    }
}

/// Split completed and the parent quantity is now zero.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SplitResponse {
    pub success: bool,
}

/// Split a stock lot into child lots.
async fn split(ctx: Ctx, Staff(user): Staff, Req(req): Req<SplitRequest>) -> ApiResult<Json<SplitResponse>> {
    let mut tx = ctx.begin().await?;
    let parent = find_for_update(&mut tx, req.id).await?;
    if parent.quantity == 0 {
        return Err(ApiError::conflict("The parent stock has already split."));
    }

    let children: Vec<MedicationStock> = req
        .splits
        .unwrap_or_default()
        .into_iter()
        .map(|split| MedicationStock {
            id: 0,
            medication_brand_id: parent.medication_brand_id,
            quantity: split.quantity,
            expiry: parent.expiry,
            location: split.location.trim().to_owned(),
            stock_status: split.stock_status,
            remarks: non_blank(split.remarks.as_deref()),
        })
        .collect();
    if children.iter().map(|child| i64::from(child.quantity)).sum::<i64>() != i64::from(parent.quantity) {
        return Err(ApiError::conflict("Child stock quantity does not equal parent stock quantity!"));
    }
    let distinct: HashSet<_> = children
        .iter()
        .map(|child| (child.location.as_str(), child.stock_status, child.quantity, child.remarks.as_deref().unwrap_or("")))
        .collect();
    if distinct.len() != children.len() {
        return Err(ApiError::conflict("Splits are not distinct!"));
    }

    let now = Ts::now();
    let mut created = Vec::with_capacity(children.len());
    for child in children {
        created.push(tx.insert(child).await?);
    }
    let mut emptied = parent.clone();
    emptied.quantity = 0;
    let emptied = tx.update(&parent, emptied).await?;
    for change in stock_change_log::for_update(&Snapshot::of(&parent), &emptied, user.id, now) {
        tx.insert(change).await?;
    }
    for child in &created {
        for change in stock_change_log::for_create(child, user.id, now) {
            tx.insert(change).await?;
        }
    }
    tx.commit().await?;
    Ok(Json(SplitResponse { success: true }))
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/medication-stock", get(list).post(create))
        .route("/medication-stock/with-brand", get(list_with_brand))
        .route("/medication-stock/{id}", patch(update))
        .route("/medication-stock/{id}/adjustments", post(adjust))
        .route("/medication-stock/{id}/splits", post(split))
}

//! Builds the `StockChange` rows for a stock lot being created or edited, one row per
//! field that changed. They are written in the same transaction as the stock edit, so
//! the current staff user is recorded with them.

use uuid::Uuid;

use super::{MedicationStock, StockChange, StockChangeField, StockStatus};
use crate::time::Ts;

/// The audited fields of a lot before it is edited.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Snapshot {
    pub quantity: i32,
    pub location: String,
    pub stock_status: StockStatus,
    pub remarks: Option<String>,
}

impl Snapshot {
    pub fn of(stock: &MedicationStock) -> Self {
        Snapshot {
            quantity: stock.quantity,
            location: stock.location.clone(),
            stock_status: stock.stock_status,
            remarks: stock.remarks.clone(),
        }
    }
}

fn change(stock: &MedicationStock, field: StockChangeField, previous: &str, next: &str, user_id: Uuid, at: Ts) -> StockChange {
    StockChange {
        id: 0,
        stock_id: stock.id,
        field,
        previous_value: previous.to_owned(),
        new_value: next.to_owned(),
        user_id,
        created_at: at,
    }
}

/// Rows for a new lot: its starting quantity, plus any location, status and remarks.
/// `stock` must already be inserted (the rows reference its id).
pub fn for_create(stock: &MedicationStock, user_id: Uuid, at: Ts) -> Vec<StockChange> {
    let mut rows = vec![change(stock, StockChangeField::Quantity, "0", &stock.quantity.to_string(), user_id, at)];
    if !stock.location.is_empty() {
        rows.push(change(stock, StockChangeField::Location, "", &stock.location, user_id, at));
    }
    // The status's wire name (e.g. "active"), matching what the API returns.
    rows.push(change(stock, StockChangeField::StockStatus, "", stock.stock_status.wire(), user_id, at));
    if let Some(remarks) = stock.remarks.as_deref().filter(|remarks| !remarks.is_empty()) {
        rows.push(change(stock, StockChangeField::Remarks, "", remarks, user_id, at));
    }
    rows
}

/// Rows for each audited field that differs between `before` and the lot now.
pub fn for_update(before: &Snapshot, stock: &MedicationStock, user_id: Uuid, at: Ts) -> Vec<StockChange> {
    let mut rows = Vec::new();
    if before.location != stock.location {
        rows.push(change(stock, StockChangeField::Location, &before.location, &stock.location, user_id, at));
    }
    if before.quantity != stock.quantity {
        rows.push(change(
            stock,
            StockChangeField::Quantity,
            &before.quantity.to_string(),
            &stock.quantity.to_string(),
            user_id,
            at,
        ));
    }
    if before.stock_status != stock.stock_status {
        rows.push(change(
            stock,
            StockChangeField::StockStatus,
            before.stock_status.wire(),
            stock.stock_status.wire(),
            user_id,
            at,
        ));
    }
    if before.remarks != stock.remarks {
        rows.push(change(
            stock,
            StockChangeField::Remarks,
            before.remarks.as_deref().unwrap_or(""),
            stock.remarks.as_deref().unwrap_or(""),
            user_id,
            at,
        ));
    }
    rows
}

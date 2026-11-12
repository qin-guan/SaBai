//! Pharmacy: ingredients, brands, stock lots, orders, the pharmacy queue and history.

mod brands;
mod flows;
mod ingredients;
mod order_edges;
mod stock;

use serde_json::{Value, json};

use crate::support::*;

/// An expiry date this far from now, as the API reads it.
fn expiry_in_days(days: i64) -> String {
    (chrono::Utc::now() + chrono::Duration::days(days)).to_rfc3339()
}

async fn create_ingredient(client: &mut Client, name: &str, fall_below: i64) -> i64 {
    let ingredient = client
        .post("/medication-ingredients", json!({ "name": name, "unitOfMeasurement": "tablets", "fallBelow": fall_below }))
        .await
        .ok()
        .json();
    id(&ingredient)
}

async fn create_brand(client: &mut Client, name: &str, ingredient_id: i64) -> i64 {
    let brand = client
        .post("/medication-brands", json!({ "name": name, "activeIngredientId": ingredient_id, "remarks": "" }))
        .await
        .ok()
        .json();
    id(&brand)
}

/// A brand with an ingredient of its own (low-stock threshold 5): the brand's id.
async fn create_brand_with_ingredient(client: &mut Client, label: &str) -> i64 {
    let ingredient_id = create_ingredient(client, &format!("{label} ingredient"), 5).await;
    create_brand(client, &format!("{label} brand"), ingredient_id).await
}

async fn create_stock_expiring(client: &mut Client, brand_id: i64, quantity: i64, location: &str, days: i64) -> i64 {
    let stock = client
        .post(
            "/medication-stock",
            json!({
                "medicationBrandId": brand_id,
                "quantity": quantity,
                "expiry": expiry_in_days(days),
                "location": location,
                "stockStatus": "active",
            }),
        )
        .await
        .ok()
        .json();
    id(&stock)
}

/// An active lot of the brand expiring in a year: its id.
async fn create_stock(client: &mut Client, brand_id: i64, quantity: i64, location: &str) -> i64 {
    create_stock_expiring(client, brand_id, quantity, location, 365).await
}

/// A consult on a new patient's visit: its id.
async fn create_consult_id(client: &mut Client) -> i64 {
    let (_, visit_id) = patient_with_visit(client).await;
    id(&create_consult(client, visit_id).await)
}

async fn create_order(client: &mut Client, consult_id: i64, brand_id: i64, quantity: i64) -> i64 {
    let order = client
        .post(
            "/orders",
            json!({
                "consultId": consult_id,
                "medicationBrandId": brand_id,
                "quantity": quantity,
                "dosageInstructions": "1 tablet daily",
            }),
        )
        .await
        .ok()
        .json();
    id(&order)
}

/// The row with this id in a JSON list.
#[track_caller]
fn find(list: &Value, wanted: i64) -> &Value {
    list.as_array()
        .and_then(|rows| rows.iter().find(|row| row["id"] == wanted))
        .unwrap_or_else(|| panic!("{wanted} was not listed: {list}"))
}

/// The lot as `GET /medication-stock` lists it; panics when it is not listed (empty lots are not).
async fn listed_lot(client: &mut Client, stock_id: i64) -> Value {
    let listed = client.get("/medication-stock").await.ok().json();
    find(&listed, stock_id).clone()
}

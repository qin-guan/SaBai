use reqwest::StatusCode;
use serde_json::json;

use super::*;

#[tokio::test]
async fn order_for_an_unknown_consult_is_not_found() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let brand_id = create_brand_with_ingredient(&mut client, &format!("Order {}", suffix())).await;

    let response = client
        .post(
            "/orders",
            json!({ "consultId": 99999999, "medicationBrandId": brand_id, "quantity": 1, "dosageInstructions": "1 tablet daily" }),
        )
        .await
        .expect(StatusCode::NOT_FOUND);
    assert_eq!(response.detail(), "Consult was not found.");
}

#[tokio::test]
async fn order_for_nothing_is_rejected() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let brand_id = create_brand_with_ingredient(&mut client, &format!("Order {}", suffix())).await;
    let consult_id = create_consult_id(&mut client).await;

    let response = client
        .post(
            "/orders",
            json!({ "consultId": consult_id, "medicationBrandId": brand_id, "quantity": 0, "dosageInstructions": "1 tablet daily" }),
        )
        .await
        .expect(StatusCode::BAD_REQUEST);
    assert_eq!(response.errors(), [("quantity".to_owned(), "'quantity' must be greater than '0'.".to_owned())]);
}

#[tokio::test]
async fn approving_more_than_is_in_stock_is_rejected() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let suffix = suffix();
    let brand_id = create_brand_with_ingredient(&mut client, &format!("Order {suffix}")).await;
    let stock_id = create_stock(&mut client, brand_id, 1, &format!("short-{suffix}")).await;
    let consult_id = create_consult_id(&mut client).await;
    let order_id = create_order(&mut client, consult_id, brand_id, 5).await;

    let response = client
        .patch(&format!("/orders/{order_id}"), json!({ "status": "APPROVED", "dosageInstructions": "changed" }))
        .await
        .expect(StatusCode::BAD_REQUEST);
    assert_eq!(response.detail(), "Medicine stock cannot be negative.");

    // Nothing of the refused approval is kept: not the notes, not a deduction.
    let order = client.get(&format!("/orders/{order_id}")).await.ok().json();
    assert_eq!(order["status"], "PENDING");
    assert_eq!(order["dosageInstructions"], "1 tablet daily");
    assert_eq!(listed_lot(&mut client, stock_id).await["quantity"], 1);
}

#[tokio::test]
async fn approving_twice_is_rejected() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let suffix = suffix();
    let brand_id = create_brand_with_ingredient(&mut client, &format!("Order {suffix}")).await;
    create_stock(&mut client, brand_id, 10, &format!("again-{suffix}")).await;
    let consult_id = create_consult_id(&mut client).await;
    let order_id = create_order(&mut client, consult_id, brand_id, 2).await;
    client.patch(&format!("/orders/{order_id}"), json!({ "status": "APPROVED" })).await.ok();

    let second =
        client.patch(&format!("/orders/{order_id}"), json!({ "status": "APPROVED" })).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(second.detail(), "Order is already APPROVED.");
}

#[tokio::test]
async fn cancelling_leaves_the_stock_alone() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let suffix = suffix();
    let brand_id = create_brand_with_ingredient(&mut client, &format!("Order {suffix}")).await;
    let location = format!("cancel-{suffix}");
    let stock_id = create_stock(&mut client, brand_id, 10, &location).await;
    let consult_id = create_consult_id(&mut client).await;
    let order_id = create_order(&mut client, consult_id, brand_id, 2).await;

    client.patch(&format!("/orders/{order_id}"), json!({ "status": "CANCELLED" })).await.expect(StatusCode::OK);

    let listed = client.get("/medication-stock").await.expect(StatusCode::OK);
    assert!(listed.text().contains(&location));
    assert_eq!(find(&listed.json(), stock_id)["quantity"], 10);

    let again =
        client.patch(&format!("/orders/{order_id}"), json!({ "status": "PENDING" })).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(again.detail(), "Order is already CANCELLED.");
}

#[tokio::test]
async fn pharmacy_queue_names_the_medication() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let suffix = suffix();
    let medication_name = format!("Queue med {suffix}");
    let ingredient_id = create_ingredient(&mut client, &format!("Order ingredient {suffix}"), 5).await;
    let brand_id = create_brand(&mut client, &medication_name, ingredient_id).await;
    let consult_id = create_consult_id(&mut client).await;
    create_order(&mut client, consult_id, brand_id, 1).await;

    let queue = client.get("/pharmacy-orders").await.expect(StatusCode::OK);
    assert!(queue.text().contains(&medication_name));
}

#[tokio::test]
async fn unknown_order_is_not_found() {
    let mut client = Client::signed_in(PHARMACIST).await;

    let response = client.get("/orders/99999999").await.expect(StatusCode::NOT_FOUND);
    assert_eq!(response.detail(), "Order was not found.");
}

#[tokio::test]
async fn orders_need_a_session() {
    let mut client = Client::anonymous();

    client
        .post(
            "/orders",
            json!({ "consultId": 1, "medicationBrandId": 1, "quantity": 1, "dosageInstructions": "1 tablet daily" }),
        )
        .await
        .expect(StatusCode::UNAUTHORIZED);
}

#[tokio::test]
async fn approval_takes_from_the_lots_that_expire_first_and_links_the_changes() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let suffix = suffix();
    let brand_id = create_brand_with_ingredient(&mut client, &format!("Lots {suffix}")).await;
    let late = create_stock_expiring(&mut client, brand_id, 4, &format!("late-{suffix}"), 700).await;
    let early = create_stock_expiring(&mut client, brand_id, 3, &format!("early-{suffix}"), 30).await;
    let reserved = create_stock_expiring(&mut client, brand_id, 50, &format!("reserved-{suffix}"), 10).await;
    client.patch(&format!("/medication-stock/{reserved}"), json!({ "stockStatus": "reserved" })).await.ok();
    let consult_id = create_consult_id(&mut client).await;
    let order_id = create_order(&mut client, consult_id, brand_id, 5).await;

    client
        .patch(&format!("/orders/{order_id}"), json!({ "status": "APPROVED", "remarks": " handed over " }))
        .await
        .expect(StatusCode::OK);

    // The early lot is used up (and no longer listed), the rest comes from the late one;
    // reserved stock is not touched.
    let listed = client.get("/medication-stock").await.ok().json();
    assert!(!listed.as_array().unwrap().iter().any(|lot| lot["id"] == early));
    assert_eq!(find(&listed, late)["quantity"], 2);
    assert_eq!(find(&listed, reserved)["quantity"], 50);

    let pool = db().await;
    let status: String = sqlx::query_scalar(r#"SELECT "StockStatus" FROM "MedicationStocks" WHERE "Id" = $1"#)
        .bind(early as i32)
        .fetch_one(&pool)
        .await
        .unwrap();
    assert_eq!(status, "Dispensed");
    let linked: Vec<(i32, String, String, String)> = sqlx::query_as(
        r#"SELECT sc."StockId", sc."Field", sc."PreviousValue", sc."NewValue"
           FROM "OrderItems" oi JOIN "StockChanges" sc ON sc."Id" = oi."StockChangeId"
           WHERE oi."OrderId" = $1 ORDER BY sc."Id""#,
    )
    .bind(order_id as i32)
    .fetch_all(&pool)
    .await
    .unwrap();
    assert_eq!(
        linked,
        [
            (early as i32, "Quantity".to_owned(), "3".to_owned(), "0".to_owned()),
            (late as i32, "Quantity".to_owned(), "4".to_owned(), "2".to_owned()),
        ]
    );

    let order = client.get(&format!("/orders/{order_id}")).await.ok().json();
    assert_eq!(order["remarks"], "handed over");

    // Deleting the order does not put the stock back.
    client.delete(&format!("/orders/{order_id}")).await.expect(StatusCode::NO_CONTENT);
    client.get(&format!("/orders/{order_id}")).await.expect(StatusCode::NOT_FOUND);
    assert_eq!(listed_lot(&mut client, late).await["quantity"], 2);
}

#[tokio::test]
async fn orders_are_listed_newest_first_and_filtered_by_status() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let brand_id = create_brand_with_ingredient(&mut client, &format!("List {}", suffix())).await;
    let consult_id = create_consult_id(&mut client).await;
    let first = create_order(&mut client, consult_id, brand_id, 1).await;
    let second = create_order(&mut client, consult_id, brand_id, 2).await;
    client.patch(&format!("/orders/{second}"), json!({ "status": "CANCELLED" })).await.ok();

    let mine = |orders: serde_json::Value| -> Vec<i64> {
        orders.as_array().unwrap().iter().filter(|order| order["consultId"] == consult_id).map(id).collect()
    };
    assert_eq!(mine(client.get("/orders").await.ok().json()), [second, first]);
    assert_eq!(mine(client.get("/orders?status=%20pending%20").await.ok().json()), [first]);
    assert_eq!(mine(client.get("/orders?status=CANCELLED").await.ok().json()), [second]);

    let invalid = client.get("/orders?status=bogus").await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(invalid.detail(), "status must be PENDING, APPROVED, or CANCELLED.");
}

#[tokio::test]
async fn approvals_at_the_same_time_never_hand_out_more_than_is_in_stock() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let suffix = suffix();
    let brand_id = create_brand_with_ingredient(&mut client, &format!("Race {suffix}")).await;
    let first_lot = create_stock_expiring(&mut client, brand_id, 4, &format!("race-a-{suffix}"), 30).await;
    let second_lot = create_stock_expiring(&mut client, brand_id, 6, &format!("race-b-{suffix}"), 60).await;
    let consult_id = create_consult_id(&mut client).await;

    // Stock for three of these eight orders (3 × 3 = 9 of 10), each approved twice at once.
    let mut order_ids = Vec::new();
    for _ in 0..8 {
        order_ids.push(create_order(&mut client, consult_id, brand_id, 3).await);
    }
    let mut attempts = Vec::new();
    for order_id in order_ids.iter().chain(&order_ids) {
        let request =
            client.request(reqwest::Method::PATCH, &format!("/orders/{order_id}")).json(&json!({ "status": "APPROVED" }));
        attempts.push(tokio::spawn(async move { request.send().await.unwrap().status() }));
    }
    let mut approved = 0;
    for attempt in attempts {
        match attempt.await.unwrap() {
            StatusCode::OK => approved += 1,
            StatusCode::BAD_REQUEST => {}
            other => panic!("unexpected status {other}"),
        }
    }
    assert_eq!(approved, 3);

    let orders = client.get("/orders?status=APPROVED").await.ok().json();
    let approved_orders = orders.as_array().unwrap().iter().filter(|order| order["consultId"] == consult_id).count();
    assert_eq!(approved_orders, 3);

    let pool = db().await;
    let left: Vec<(i32, i32)> =
        sqlx::query_as(r#"SELECT "Id", "Quantity" FROM "MedicationStocks" WHERE "MedicationBrandId" = $1 ORDER BY "Id""#)
            .bind(brand_id as i32)
            .fetch_all(&pool)
            .await
            .unwrap();
    assert_eq!(left, [(first_lot as i32, 0), (second_lot as i32, 1)]);
    // Every unit that left a lot is accounted for by a change linked to an approved order.
    let taken: i64 = sqlx::query_scalar(
        r#"SELECT COALESCE(SUM(sc."PreviousValue"::int - sc."NewValue"::int), 0)::int8
           FROM "OrderItems" oi
           JOIN "StockChanges" sc ON sc."Id" = oi."StockChangeId"
           JOIN "Orders" o ON o."Id" = oi."OrderId"
           WHERE o."MedicationBrandId" = $1 AND o."Status" = 'Approved'"#,
    )
    .bind(brand_id as i32)
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(taken, 9);
}

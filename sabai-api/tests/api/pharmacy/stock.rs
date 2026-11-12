use reqwest::StatusCode;
use serde_json::json;

use super::*;

/// A lot of a new brand: `(stock id, brand id)`.
async fn create_stock_with_brand(client: &mut Client, label: &str, quantity: i64, location: &str) -> (i64, i64) {
    let brand_id = create_brand_with_ingredient(client, &format!("Stock {label}")).await;
    (create_stock(client, brand_id, quantity, location).await, brand_id)
}

#[tokio::test]
async fn adjusting_up_shows_the_new_quantity() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let suffix = suffix();
    let location = format!("adjust-{suffix}");
    let (stock_id, _) = create_stock_with_brand(&mut client, &suffix, 5, &location).await;

    let adjusted = client
        .post(&format!("/medication-stock/{stock_id}/adjustments"), json!({ "quantityChange": 2 }))
        .await
        .expect(StatusCode::OK);
    assert_eq!(adjusted.json()["quantity"], 7);

    let listed = client.get("/medication-stock").await.expect(StatusCode::OK);
    assert!(listed.text().contains(&location));
    assert_eq!(find(&listed.json(), stock_id)["quantity"], 7);
}

#[tokio::test]
async fn adjusting_below_zero_is_rejected_and_leaves_the_quantity() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let suffix = suffix();
    let location = format!("floor-{suffix}");
    let (stock_id, _) = create_stock_with_brand(&mut client, &suffix, 3, &location).await;

    let adjusted = client
        .post(&format!("/medication-stock/{stock_id}/adjustments"), json!({ "quantityChange": -4 }))
        .await
        .expect(StatusCode::BAD_REQUEST);
    assert_eq!(adjusted.detail(), "Stock quantity cannot be negative.");

    let listed = client.get("/medication-stock").await.expect(StatusCode::OK);
    assert!(listed.text().contains(&location));
    assert_eq!(find(&listed.json(), stock_id)["quantity"], 3);
}

#[tokio::test]
async fn stock_of_an_unknown_brand_is_not_found() {
    let mut client = Client::signed_in(PHARMACIST).await;

    let response = client
        .post(
            "/medication-stock",
            json!({
                "medicationBrandId": 99999999,
                "quantity": 1,
                "expiry": expiry_in_days(30),
                "location": format!("missing-{}", suffix()),
                "stockStatus": "active",
            }),
        )
        .await
        .expect(StatusCode::NOT_FOUND);
    assert_eq!(response.detail(), "Medication brand was not found.");
}

#[tokio::test]
async fn updating_unknown_stock_is_not_found() {
    let mut client = Client::signed_in(PHARMACIST).await;

    let response = client
        .patch("/medication-stock/99999999", json!({ "location": format!("missing-{}", suffix()) }))
        .await
        .expect(StatusCode::NOT_FOUND);
    assert_eq!(response.detail(), "Medication stock was not found.");
}

#[tokio::test]
async fn split_into_a_single_child_is_rejected() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let suffix = suffix();
    let (stock_id, _) = create_stock_with_brand(&mut client, &suffix, 4, &format!("parent-{suffix}")).await;

    let response = client
        .post(
            &format!("/medication-stock/{stock_id}/splits"),
            json!({ "splits": [{ "quantity": 1, "location": format!("only-{suffix}"), "stockStatus": "active" }] }),
        )
        .await
        .expect(StatusCode::BAD_REQUEST);
    assert_eq!(
        response.errors(),
        [("splits".to_owned(), "The specified condition was not met for 'splits'.".to_owned())]
    );
}

#[tokio::test]
async fn updated_location_is_listed() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let suffix = suffix();
    let (stock_id, _) = create_stock_with_brand(&mut client, &suffix, 2, &format!("shelf-{suffix}")).await;
    let updated_location = format!("moved-{suffix}");

    client
        .patch(&format!("/medication-stock/{stock_id}"), json!({ "location": updated_location }))
        .await
        .expect(StatusCode::OK);

    let listed = client.get("/medication-stock").await.expect(StatusCode::OK);
    assert!(listed.text().contains(&updated_location));
    assert_eq!(find(&listed.json(), stock_id)["location"], updated_location);
}

#[tokio::test]
async fn creating_and_splitting_record_quantity_history() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let suffix = suffix();
    let (stock_id, brand_id) = create_stock_with_brand(&mut client, &suffix, 6, &format!("history-{suffix}")).await;

    client
        .post(
            &format!("/medication-stock/{stock_id}/splits"),
            json!({
                "splits": [
                    { "quantity": 4, "location": format!("left-{suffix}"), "stockStatus": "active" },
                    { "quantity": 2, "location": format!("right-{suffix}"), "stockStatus": "active" },
                ],
            }),
        )
        .await
        .expect(StatusCode::OK);

    let history = client.get(&format!("/medication-history?brandId={brand_id}")).await.ok().json();
    let mut adjustments: Vec<i64> = history
        .as_array()
        .unwrap()
        .iter()
        .filter(|row| row["kind"] == "adjustment")
        .map(|row| row["quantityChanged"].as_i64().unwrap())
        .collect();
    adjustments.sort_unstable();
    assert_eq!(adjustments, [-6, 2, 4, 6]);
    for row in history.as_array().unwrap() {
        assert_eq!(row["actorName"], "Pharmacist");
        assert_eq!(row["patientName"], "-");
        assert_eq!(row["quantityRemaining"], 6);
    }
}

#[tokio::test]
async fn lots_at_or_below_the_threshold_are_flagged_low() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let suffix = suffix();
    let (low_id, _) = create_stock_with_brand(&mut client, &format!("low-{suffix}"), 5, &format!("low-{suffix}")).await;
    let (ok_id, _) = create_stock_with_brand(&mut client, &format!("ok-{suffix}"), 6, &format!("ok-{suffix}")).await;

    let listed = client.get("/medication-stock/with-brand").await.ok().json();
    assert_eq!(find(&listed, low_id)["isLowStock"], true);
    assert_eq!(find(&listed, ok_id)["isLowStock"], false);
    assert_eq!(find(&listed, low_id)["brandName"], format!("Stock low-{suffix} brand"));
    assert_eq!(find(&listed, low_id)["ingredientName"], format!("Stock low-{suffix} ingredient"));
    assert_eq!(find(&listed, low_id)["fallBelow"], 5);
}

#[tokio::test]
async fn every_edit_of_a_lot_is_written_to_the_change_log() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let me = client.who_am_i().await;
    let suffix = suffix();
    let brand_id = create_brand_with_ingredient(&mut client, &format!("Log {suffix}")).await;

    let created = client
        .post(
            "/medication-stock",
            json!({
                "medicationBrandId": brand_id,
                "quantity": 10,
                "expiry": "2031-02-03T04:05:06.123456+00:00",
                "location": " shelf ",
                "remarks": " lot ",
            }),
        )
        .await
        .expect(StatusCode::CREATED);
    assert_eq!(created.header("location"), Some("/medication-stock"));
    let stock = created.json();
    let stock_id = id(&stock);
    assert_eq!(
        stock,
        json!({
            "id": stock_id, "medicationBrandId": brand_id, "quantity": 10, "expiry": "2031-02-03T04:05:06.123456+00:00",
            "location": "shelf", "stockStatus": "active", "remarks": "lot",
        })
    );

    let path = format!("/medication-stock/{stock_id}");
    // A blank location is left alone; remarks that are not sent are cleared.
    let moved = client.patch(&path, json!({ "location": " ", "stockStatus": "reserved" })).await.ok().json();
    assert_eq!(moved["location"], "shelf");
    assert_eq!(moved["stockStatus"], "reserved");
    assert_eq!(moved["remarks"], json!(null));
    let adjusted = client.post(&format!("{path}/adjustments"), json!({ "quantityChange": -3, "remarks": "count" })).await;
    assert_eq!(adjusted.ok().json()["quantity"], 7);
    // An adjustment without remarks keeps them.
    let again = client.post(&format!("{path}/adjustments"), json!({})).await.ok().json();
    assert_eq!(again["remarks"], "count");

    let rows: Vec<(String, String, String, uuid::Uuid)> = sqlx::query_as(
        r#"SELECT "Field", "PreviousValue", "NewValue", "UserId" FROM "StockChanges" WHERE "StockId" = $1 ORDER BY "Id""#,
    )
    .bind(stock_id as i32)
    .fetch_all(&db().await)
    .await
    .unwrap();
    assert!(rows.iter().all(|row| row.3.to_string() == me["id"].as_str().unwrap()));
    let rows: Vec<(&str, &str, &str)> = rows.iter().map(|row| (row.0.as_str(), row.1.as_str(), row.2.as_str())).collect();
    assert_eq!(
        rows,
        [
            ("Quantity", "0", "10"),
            ("Location", "", "shelf"),
            ("StockStatus", "", "active"),
            ("Remarks", "", "lot"),
            ("StockStatus", "active", "reserved"),
            ("Remarks", "lot", ""),
            ("Quantity", "10", "7"),
            ("Remarks", "", "count"),
        ]
    );
}

#[tokio::test]
async fn invalid_stock_requests_are_rejected_with_the_reasons() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let suffix = suffix();
    let (stock_id, _) = create_stock_with_brand(&mut client, &suffix, 10, &format!("rules-{suffix}")).await;

    let invalid = client
        .post("/medication-stock", json!({ "medicationBrandId": 0, "quantity": -1, "location": "" }))
        .await
        .expect(StatusCode::BAD_REQUEST);
    assert_eq!(
        invalid.errors(),
        [
            ("medicationBrandId".to_owned(), "'medication Brand Id' must be greater than '0'.".to_owned()),
            ("quantity".to_owned(), "'quantity' must be greater than or equal to '0'.".to_owned()),
            ("expiry".to_owned(), "'expiry' must not be equal to '01/01/0001 00:00:00 +00:00'.".to_owned()),
            ("location".to_owned(), "'location' must not be empty.".to_owned()),
        ]
    );

    let splits = format!("/medication-stock/{stock_id}/splits");
    let null_splits = client.post(&splits, json!({ "splits": null })).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(null_splits.errors(), [("splits".to_owned(), "'splits' must not be empty.".to_owned())]);
    let bad_children = client
        .post(&splits, json!({ "splits": [{ "quantity": 0, "location": "" }, { "quantity": 1, "location": "a" }] }))
        .await
        .expect(StatusCode::BAD_REQUEST);
    assert_eq!(
        bad_children.errors(),
        [
            ("splits[0].quantity".to_owned(), "'quantity' must be greater than '0'.".to_owned()),
            ("splits[0].location".to_owned(), "'location' must not be empty.".to_owned()),
        ]
    );

    let same = json!({ "quantity": 5, "location": "a", "stockStatus": "active" });
    let duplicates = client.post(&splits, json!({ "splits": [same, same] })).await.expect(StatusCode::CONFLICT);
    assert_eq!(duplicates.detail(), "Splits are not distinct!");

    let halves = json!({ "splits": [
        { "quantity": 5, "location": "a", "stockStatus": "active" },
        { "quantity": 5, "location": "b", "stockStatus": "active" },
    ] });
    client.post(&splits, halves.clone()).await.expect(StatusCode::OK);
    let again = client.post(&splits, halves).await.expect(StatusCode::CONFLICT);
    assert_eq!(again.detail(), "The parent stock has already split.");
}

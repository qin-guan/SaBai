use reqwest::StatusCode;
use serde_json::json;

use super::*;

#[tokio::test]
async fn splitting_a_lot_hides_the_consumed_parent() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let suffix = suffix();
    let ingredient_id = create_ingredient(&mut client, &format!("Ingredient {suffix}"), 5).await;
    let brand_id = create_brand(&mut client, &format!("Brand {suffix}"), ingredient_id).await;
    let created = client
        .post(
            "/medication-stock",
            json!({
                "medicationBrandId": brand_id,
                "quantity": 10,
                "expiry": expiry_in_days(365),
                "location": format!("parent-{suffix}"),
                "stockStatus": "active",
                "remarks": "parent lot",
            }),
        )
        .await
        .expect(StatusCode::CREATED);
    let parent_id = id(&created.json());

    let split = client
        .post(
            &format!("/medication-stock/{parent_id}/splits"),
            json!({
                "splits": [
                    { "quantity": 6, "location": format!("child-a-{suffix}"), "stockStatus": "active", "remarks": "a" },
                    { "quantity": 4, "location": format!("child-b-{suffix}"), "stockStatus": "reserved", "remarks": "b" },
                ],
            }),
        )
        .await
        .expect(StatusCode::OK);
    assert_eq!(split.json(), json!({ "success": true }));

    let listed = client.get("/medication-stock").await.ok().json();
    let listed = listed.as_array().unwrap();
    assert!(!listed.iter().any(|lot| lot["id"] == parent_id));
    assert!(listed.iter().any(|lot| lot["location"] == format!("child-a-{suffix}") && lot["quantity"] == 6));
    assert!(listed.iter().any(|lot| lot["location"] == format!("child-b-{suffix}") && lot["quantity"] == 4));

    let with_brand = client.get("/medication-stock/with-brand").await.ok().json();
    assert!(with_brand.as_array().unwrap().iter().any(|lot| {
        lot["location"] == format!("child-a-{suffix}")
            && lot["brandName"] == format!("Brand {suffix}")
            && lot["ingredientName"] == format!("Ingredient {suffix}")
    }));
}

#[tokio::test]
async fn split_that_does_not_add_up_is_a_conflict() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let suffix = suffix();
    let ingredient_id = create_ingredient(&mut client, &format!("Mismatch {suffix}"), 2).await;
    let brand_id = create_brand(&mut client, &format!("Mismatch brand {suffix}"), ingredient_id).await;
    let parent_id = create_stock_expiring(&mut client, brand_id, 10, &format!("mismatch-{suffix}"), 180).await;

    let split = client
        .post(
            &format!("/medication-stock/{parent_id}/splits"),
            json!({
                "splits": [
                    { "quantity": 3, "location": format!("bad-a-{suffix}"), "stockStatus": "active" },
                    { "quantity": 3, "location": format!("bad-b-{suffix}"), "stockStatus": "active" },
                ],
            }),
        )
        .await
        .expect(StatusCode::CONFLICT);
    assert!(split.text().contains("Child stock quantity does not equal parent stock quantity!"));

    assert_eq!(listed_lot(&mut client, parent_id).await["quantity"], 10);
}

#[tokio::test]
async fn stock_changes_only_when_an_order_is_approved() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let me = client.who_am_i().await;
    let suffix = suffix();
    let ingredient_id = create_ingredient(&mut client, &format!("Order ingredient {suffix}"), 50).await;
    let brand_id = create_brand(&mut client, &format!("Order brand {suffix}"), ingredient_id).await;
    let stock_id = create_stock_expiring(&mut client, brand_id, 10, &format!("order-{suffix}"), 90).await;

    let code = format!("V{}", suffix.to_uppercase());
    let village = create_village_named(&mut client, &code, &format!("Village {suffix}")).await;
    let patient_name = format!("Patient {suffix}");
    let patient = register_patient_named(&mut client, id(&village), &patient_name).await;
    let display_id = patient["displayId"].as_str().unwrap().to_owned();
    assert_eq!(display_id, format!("{code}{:04}", id(&patient)));
    let visit_id = patient["visitId"].as_i64().unwrap();
    let consult = client
        .post(
            "/consults",
            json!({ "visitId": visit_id, "diagnoses": [{ "details": "cough", "category": "Respiratory" }] }),
        )
        .await
        .ok()
        .json();
    let consult_id = id(&consult);

    let created = client
        .post(
            "/orders",
            json!({
                "consultId": consult_id,
                "medicationBrandId": brand_id,
                "quantity": 4,
                "dosageInstructions": "1 tablet daily",
                "remarks": "with food",
            }),
        )
        .await
        .expect(StatusCode::CREATED);
    let order = created.json();
    let order_id = id(&order);
    assert_eq!(created.header("location"), Some(format!("/orders/{order_id}").as_str()));
    assert_eq!(
        order,
        json!({
            "id": order_id, "consultId": consult_id, "medicationBrandId": brand_id,
            "brandName": format!("Order brand {suffix}"), "quantity": 4, "status": "PENDING",
            "dosageInstructions": "1 tablet daily", "remarks": "with food", "approvedById": null,
            "approvedAt": null, "createdAt": order["createdAt"],
        })
    );
    assert_eq!(listed_lot(&mut client, stock_id).await["quantity"], 10);

    let queue = client.get("/pharmacy-orders").await.expect(StatusCode::OK);
    let queue_body = queue.text();
    assert!(queue_body.contains(&patient_name));
    assert!(queue_body.contains(&format!("Order brand {suffix}")));
    assert!(queue_body.contains("cough"));
    assert!(queue_body.contains(&display_id));
    let queue = queue.json();
    let entry = queue.as_array().unwrap().iter().find(|entry| entry["patient"]["displayId"] == display_id).unwrap();
    assert_eq!(
        *entry,
        json!({
            "patient": { "displayId": display_id, "name": patient_name, "villageCode": code },
            "visits": [{
                "visitId": visit_id,
                "visitDate": entry["visits"][0]["visitDate"],
                "orders": [{
                    "orderId": order_id, "medicationName": format!("Order brand {suffix}"), "quantity": 4,
                    "isLowStock": true, "dosageInstructions": "1 tablet daily",
                }],
                "diagnoses": [{ "category": "Respiratory", "details": "cough" }],
            }],
        })
    );

    let approved = client.patch(&format!("/orders/{order_id}"), json!({ "status": "APPROVED" })).await;
    let approved = approved.expect(StatusCode::OK).json();
    assert_eq!(approved["status"], "APPROVED");
    assert_eq!(approved["approvedById"], me["id"]);
    assert!(approved["approvedAt"].is_string());
    assert_eq!(listed_lot(&mut client, stock_id).await["quantity"], 6);

    let too_large = client
        .post(
            "/orders",
            json!({ "consultId": consult_id, "medicationBrandId": brand_id, "quantity": 100, "dosageInstructions": "too much" }),
        )
        .await
        .ok()
        .json();
    let over_approve = client
        .patch(&format!("/orders/{}", id(&too_large)), json!({ "status": "APPROVED" }))
        .await
        .expect(StatusCode::BAD_REQUEST);
    assert!(over_approve.text().contains("Medicine stock cannot be negative."));
    assert_eq!(listed_lot(&mut client, stock_id).await["quantity"], 6);

    let to_cancel = client
        .post(
            "/orders",
            json!({ "consultId": consult_id, "medicationBrandId": brand_id, "quantity": 2, "dosageInstructions": "cancel me" }),
        )
        .await
        .ok()
        .json();
    let cancelled = client
        .patch(&format!("/orders/{}", id(&to_cancel)), json!({ "status": "CANCELLED" }))
        .await
        .expect(StatusCode::OK);
    assert_eq!(cancelled.json()["status"], "CANCELLED");
    assert_eq!(listed_lot(&mut client, stock_id).await["quantity"], 6);

    let second_approve =
        client.patch(&format!("/orders/{order_id}"), json!({ "status": "APPROVED" })).await.expect(StatusCode::BAD_REQUEST);
    assert!(second_approve.text().contains("Order is already APPROVED."));

    // The dispense is in the brand's history, under the patient's name.
    let history = client.get(&format!("/medication-history?brandId={brand_id}")).await.ok().json();
    let dispense = history.as_array().unwrap().iter().find(|row| row["kind"] == "order").unwrap();
    assert_eq!(
        *dispense,
        json!({
            "kind": "order", "actorName": "Pharmacist", "patientName": patient_name, "quantityChanged": -4,
            "quantityRemaining": 6, "date": approved["approvedAt"],
        })
    );
}

#[tokio::test]
async fn medication_history_needs_a_brand() {
    let mut client = Client::signed_in(PHARMACIST).await;

    let response = client.get("/medication-history").await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(response.detail(), "brandId is required.");
}

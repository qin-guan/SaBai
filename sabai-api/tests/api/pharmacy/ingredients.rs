use reqwest::StatusCode;
use serde_json::json;

use super::*;

#[tokio::test]
async fn ingredient_is_created_listed_and_renamed() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let suffix = suffix();
    let name = format!("Ingredient {suffix}");
    let updated_name = format!("Revised {suffix}");

    let created = client
        .post("/medication-ingredients", json!({ "name": name, "unitOfMeasurement": "tablets", "fallBelow": 5 }))
        .await
        .expect(StatusCode::CREATED);
    assert_eq!(created.header("location"), Some("/medication-ingredients"));
    let ingredient = created.json();
    let ingredient_id = id(&ingredient);
    assert_eq!(
        ingredient,
        json!({ "id": ingredient_id, "name": name, "unitOfMeasurement": "tablets", "fallBelow": 5, "remarks": null })
    );

    let listed = client.get("/medication-ingredients").await.expect(StatusCode::OK);
    assert!(listed.text().contains(&name));

    let updated = client
        .put(
            &format!("/medication-ingredients/{ingredient_id}"),
            json!({ "name": updated_name, "unitOfMeasurement": "tablets", "fallBelow": 5 }),
        )
        .await
        .expect(StatusCode::OK);
    assert_eq!(updated.json()["name"], updated_name);

    let after = client.get("/medication-ingredients").await.expect(StatusCode::OK);
    assert!(after.text().contains(&updated_name));
    assert_eq!(find(&after.json(), ingredient_id)["name"], updated_name);
}

#[tokio::test]
async fn ingredient_with_a_zero_threshold_is_rejected() {
    let mut client = Client::signed_in(PHARMACIST).await;

    let response = client
        .post(
            "/medication-ingredients",
            json!({ "name": format!("Zero {}", suffix()), "unitOfMeasurement": "tablets", "fallBelow": 0 }),
        )
        .await
        .expect(StatusCode::BAD_REQUEST);
    assert_eq!(response.errors(), [("fallBelow".to_owned(), "'fall Below' must be greater than '0'.".to_owned())]);
}

#[tokio::test]
async fn updating_an_unknown_ingredient_is_not_found() {
    let mut client = Client::signed_in(PHARMACIST).await;

    let response = client
        .put(
            "/medication-ingredients/99999999",
            json!({ "name": format!("Missing {}", suffix()), "unitOfMeasurement": "tablets", "fallBelow": 5 }),
        )
        .await
        .expect(StatusCode::NOT_FOUND);
    assert_eq!(response.detail(), "Medication ingredient was not found.");
}

#[tokio::test]
async fn ingredient_used_by_a_brand_cannot_be_deleted() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let suffix = suffix();
    let ingredient_id = create_ingredient(&mut client, &format!("Linked {suffix}"), 5).await;
    create_brand(&mut client, &format!("Linked brand {suffix}"), ingredient_id).await;

    let response =
        client.delete(&format!("/medication-ingredients/{ingredient_id}")).await.expect(StatusCode::CONFLICT);
    assert_eq!(response.detail(), "The ingredient is referenced by a medication brand.");
}

#[tokio::test]
async fn unused_ingredient_is_deleted() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let name = format!("Unused {}", suffix());
    let ingredient_id = create_ingredient(&mut client, &name, 5).await;

    client.delete(&format!("/medication-ingredients/{ingredient_id}")).await.expect(StatusCode::NO_CONTENT);

    let listed = client.get("/medication-ingredients").await.expect(StatusCode::OK);
    assert!(!listed.text().contains(&name));
}

#[tokio::test]
async fn ingredients_need_a_session() {
    let mut client = Client::anonymous();
    client.get("/medication-ingredients").await.expect(StatusCode::UNAUTHORIZED);
}

use reqwest::StatusCode;
use serde_json::json;

use super::*;

#[tokio::test]
async fn brand_is_listed_with_its_ingredient_name() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let suffix = suffix();
    let ingredient_name = format!("Ingredient {suffix}");
    let brand_name = format!("Brand {suffix}");
    let ingredient_id = create_ingredient(&mut client, &ingredient_name, 5).await;

    let created = client
        .post("/medication-brands", json!({ "name": brand_name, "activeIngredientId": ingredient_id, "remarks": "" }))
        .await
        .expect(StatusCode::CREATED);
    assert_eq!(created.header("location"), Some("/medication-brands"));
    let brand_id = id(&created.json());
    assert_eq!(
        created.json(),
        json!({ "id": brand_id, "name": brand_name, "activeIngredientId": ingredient_id, "remarks": "" })
    );

    let listed = client.get("/medication-brands/with-ingredient").await.expect(StatusCode::OK);
    assert!(listed.text().contains(&ingredient_name));
    assert!(listed.text().contains(&brand_name));
    assert_eq!(
        *find(&listed.json(), brand_id),
        json!({ "id": brand_id, "name": brand_name, "activeIngredientName": ingredient_name })
    );
}

#[tokio::test]
async fn brand_of_an_unknown_ingredient_is_not_found() {
    let mut client = Client::signed_in(PHARMACIST).await;

    let response = client
        .post(
            "/medication-brands",
            json!({ "name": format!("Missing brand {}", suffix()), "activeIngredientId": 99999999, "remarks": "" }),
        )
        .await
        .expect(StatusCode::NOT_FOUND);
    assert_eq!(response.detail(), "Medication ingredient was not found.");
}

#[tokio::test]
async fn brand_with_stock_cannot_be_deleted() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let suffix = suffix();
    let ingredient_id = create_ingredient(&mut client, &format!("Stock ingredient {suffix}"), 5).await;
    let brand_id = create_brand(&mut client, &format!("Stock brand {suffix}"), ingredient_id).await;
    create_stock(&mut client, brand_id, 1, &format!("lot-{suffix}")).await;

    let response = client.delete(&format!("/medication-brands/{brand_id}")).await.expect(StatusCode::CONFLICT);
    assert_eq!(response.detail(), "The brand is referenced by medication stock.");
}

#[tokio::test]
async fn deleting_an_unknown_brand_is_not_found() {
    let mut client = Client::signed_in(PHARMACIST).await;

    let response = client.delete("/medication-brands/99999999").await.expect(StatusCode::NOT_FOUND);
    assert_eq!(response.detail(), "Medication brand was not found.");
}

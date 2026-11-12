use reqwest::StatusCode;
use serde_json::{Value, json};

use super::*;

async fn create_consult(client: &mut Client) -> i64 {
    consult_for_new_patient(client, &format!("Diagnosis {}", suffix())).await.0
}

#[tokio::test]
async fn created_diagnosis_can_be_fetched_and_is_listed_for_its_consult() {
    let mut client = Client::admin().await;
    let consult_id = create_consult(&mut client).await;

    let created = client
        .post("/diagnoses", json!({ "consultId": consult_id, "details": "wheeze", "category": "Respiratory" }))
        .await
        .expect(StatusCode::CREATED);
    let diagnosis = created.json();
    let path = format!("/diagnoses/{}", id(&diagnosis));
    let fetched = client.get(&path).await.expect(StatusCode::OK).json();
    let listed = client.get(&format!("/diagnoses?consultId={consult_id}")).await.expect(StatusCode::OK).json();

    assert_eq!(created.header("location"), Some(path.as_str()));
    assert_eq!(fetched["details"], "wheeze");
    assert_eq!(fetched["category"], "Respiratory");
    assert_eq!(fetched, json!({ "id": id(&diagnosis), "consultId": consult_id, "details": "wheeze", "category": "Respiratory" }));
    assert!(listed.as_array().unwrap().iter().any(|item| item["id"] == diagnosis["id"]));
}

#[tokio::test]
async fn diagnosis_list_is_in_id_order_and_filters_by_consult_only_when_given_one() {
    let mut client = Client::admin().await;
    let consult_id = create_consult(&mut client).await;
    let other_consult = create_consult(&mut client).await;
    let added = client.post("/diagnoses", json!({ "consultId": consult_id, "details": " trimmed " })).await.ok().json();
    assert_eq!(added["details"], "trimmed");
    assert_eq!(added["category"], Value::Null);

    let of_consult = client.get(&format!("/diagnoses?consultId={consult_id}")).await.ok().json();
    let of_consult = of_consult.as_array().unwrap();
    assert_eq!(of_consult.len(), 2);
    assert!(of_consult.iter().all(|item| item["consultId"] == consult_id));
    assert!(id(&of_consult[0]) < id(&of_consult[1]));
    assert_eq!(of_consult[1], added);

    for unfiltered in ["/diagnoses", "/diagnoses?consultId=0"] {
        let all = client.get(unfiltered).await.ok().json();
        let all = all.as_array().unwrap();
        assert!(all.iter().any(|item| item["consultId"] == consult_id));
        assert!(all.iter().any(|item| item["consultId"] == other_consult));
        assert!(all.windows(2).all(|pair| id(&pair[0]) < id(&pair[1])));
    }
    assert_eq!(client.get("/diagnoses?consultId=99999999").await.ok().json(), json!([]));
}

#[tokio::test]
async fn diagnosis_can_be_updated_then_deleted() {
    let mut client = Client::admin().await;
    let consult_id = create_consult(&mut client).await;
    let created = client
        .post("/diagnoses", json!({ "consultId": consult_id, "details": format!("open {}", suffix()), "category": "Respiratory" }))
        .await
        .ok()
        .json();
    let path = format!("/diagnoses/{}", id(&created));

    let patched = client.patch(&path, json!({ "details": "resolved" })).await.expect(StatusCode::OK).json();
    let fetched = client.get(&path).await.expect(StatusCode::OK).json();
    client.delete(&path).await.expect(StatusCode::NO_CONTENT);
    let missing = client.get(&path).await.expect(StatusCode::NOT_FOUND);

    assert_eq!(patched["details"], "resolved");
    assert_eq!(patched["category"], "Respiratory", "an omitted category is kept");
    assert_eq!(fetched, patched);
    assert_eq!(missing.detail(), "Diagnosis was not found.");
}

#[tokio::test]
async fn diagnosis_update_trims_details_keeps_a_null_category_and_rejects_blank_details() {
    let mut client = Client::admin().await;
    let consult_id = create_consult(&mut client).await;
    let created = client.post("/diagnoses", json!({ "consultId": consult_id, "details": "itch", "category": "Dermatology" })).await.ok().json();
    let path = format!("/diagnoses/{}", id(&created));

    let recategorised = client.patch(&path, json!({ "details": " rash ", "category": "Oral Health" })).await.ok().json();
    assert_eq!(recategorised["details"], "rash");
    assert_eq!(recategorised["category"], "Oral Health");

    let kept = client.patch(&path, json!({ "category": null })).await.ok().json();
    assert_eq!(kept, recategorised);

    let blank = client.patch(&path, json!({ "details": "" })).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(blank.errors(), [("details".to_owned(), "'details' must not be empty.".to_owned())]);

    client.patch("/diagnoses/99999999", json!({})).await.expect(StatusCode::NOT_FOUND);
    client.delete("/diagnoses/99999999").await.expect(StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn diagnosis_for_a_missing_consult_is_not_found() {
    let mut client = Client::admin().await;

    let response = client
        .post("/diagnoses", json!({ "consultId": 99999999, "details": format!("absent {}", suffix()), "category": "Respiratory" }))
        .await
        .expect(StatusCode::NOT_FOUND);
    assert_eq!(response.detail(), "Consult was not found.");
}

#[tokio::test]
async fn diagnosis_without_details_is_rejected() {
    let mut client = Client::admin().await;
    let consult_id = create_consult(&mut client).await;

    let response = client
        .post("/diagnoses", json!({ "consultId": consult_id, "details": "", "category": "Respiratory" }))
        .await
        .expect(StatusCode::BAD_REQUEST);
    assert_eq!(response.errors(), [("details".to_owned(), "'details' must not be empty.".to_owned())]);

    let nothing = client.post("/diagnoses", json!({})).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(
        nothing.errors(),
        [
            ("consultId".to_owned(), "'consult Id' must be greater than '0'.".to_owned()),
            ("details".to_owned(), "'details' must not be empty.".to_owned()),
        ]
    );
}

#[tokio::test]
async fn missing_diagnosis_is_not_found() {
    let mut client = Client::admin().await;

    let response = client.get("/diagnoses/99999999").await.expect(StatusCode::NOT_FOUND);
    assert_eq!(response.detail(), "Diagnosis was not found.");
}

#[tokio::test]
async fn deleting_a_diagnosis_needs_a_session() {
    let mut client = Client::admin().await;
    let consult_id = create_consult(&mut client).await;
    let created = client
        .post("/diagnoses", json!({ "consultId": consult_id, "details": format!("locked {}", suffix()), "category": "Respiratory" }))
        .await
        .ok()
        .json();
    let path = format!("/diagnoses/{}", id(&created));
    let mut anonymous = Client::anonymous();

    anonymous.delete(&path).await.expect(StatusCode::UNAUTHORIZED);
    client.get(&path).await.expect(StatusCode::OK);
}

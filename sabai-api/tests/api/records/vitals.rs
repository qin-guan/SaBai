use reqwest::StatusCode;
use serde_json::json;

use crate::support::*;

#[tokio::test]
async fn created_vitals_can_be_fetched_with_their_temperature() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;

    let created = client.post("/vitals", json!({ "visitId": visit_id, "temperature": 36.6 })).await;
    let vitals_id = id(&created.json());
    let fetched = client.get(&format!("/vitals/{vitals_id}")).await;

    assert_eq!(created.status, StatusCode::CREATED);
    assert_eq!(fetched.status, StatusCode::OK);
    assert_eq!(fetched.json()["temperature"].as_f64(), Some(36.6));
}

#[tokio::test]
async fn patient_vitals_list_includes_the_created_record() {
    let mut client = Client::admin().await;
    let (patient_id, visit_id) = patient_with_visit(&mut client).await;
    let created = client.post("/vitals", json!({ "visitId": visit_id, "temperature": 36.6 })).await.ok().json();

    let listed = client.get(&format!("/patients/{patient_id}/vitals")).await.expect(StatusCode::OK).json();

    assert!(listed.as_array().unwrap().iter().any(|item| item["id"] == created["id"]));
}

#[tokio::test]
async fn vitals_for_an_unknown_visit_are_not_found() {
    let mut client = Client::admin().await;

    let response = client.post("/vitals", json!({ "visitId": 99999999, "temperature": 36.6 })).await;

    assert_eq!(response.status, StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn vitals_by_unknown_visit_are_not_found() {
    let mut client = Client::admin().await;

    let response = client.get("/visits/99999999/vitals").await;

    assert_eq!(response.status, StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn updating_unknown_vitals_is_not_found() {
    let mut client = Client::admin().await;

    let response = client.patch("/vitals/99999999", json!({ "temperature": 37.1 })).await;

    assert_eq!(response.status, StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn recording_vitals_signed_out_is_unauthorized() {
    let mut client = Client::anonymous();

    let response = client.post("/vitals", json!({ "visitId": 1, "temperature": 36.6 })).await;

    assert_eq!(response.status, StatusCode::UNAUTHORIZED);
}

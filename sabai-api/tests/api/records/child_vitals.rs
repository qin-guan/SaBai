use reqwest::StatusCode;
use serde_json::json;

use super::create_vitals;
use crate::support::*;

#[tokio::test]
async fn created_child_vitals_are_returned_for_their_vitals() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;
    let vitals_id = create_vitals(&mut client, visit_id).await;

    let created =
        client.post("/child-vitals", json!({ "vitalsId": vitals_id, "heart": "normal", "pallor": false })).await;
    let fetched = client.get(&format!("/vitals/{vitals_id}/child-vitals")).await;

    assert_eq!(created.status, StatusCode::CREATED);
    assert_eq!(fetched.status, StatusCode::OK);
    assert_eq!(fetched.json()["heart"], "normal");
}

#[tokio::test]
async fn child_vitals_for_unknown_vitals_are_not_found() {
    let mut client = Client::admin().await;

    let response =
        client.post("/child-vitals", json!({ "vitalsId": 99999999, "heart": "normal", "pallor": false })).await;

    assert_eq!(response.status, StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn second_child_vitals_for_the_same_vitals_is_a_conflict() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;
    let vitals_id = create_vitals(&mut client, visit_id).await;
    client.post("/child-vitals", json!({ "vitalsId": vitals_id, "heart": "normal", "pallor": false })).await.ok();

    let again = client.post("/child-vitals", json!({ "vitalsId": vitals_id, "heart": "murmur", "pallor": true })).await;

    assert_eq!(again.status, StatusCode::CONFLICT);
}

#[tokio::test]
async fn updated_child_vitals_return_the_new_heart_finding() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;
    let vitals_id = create_vitals(&mut client, visit_id).await;
    let created = client
        .post("/child-vitals", json!({ "vitalsId": vitals_id, "heart": "normal", "pallor": false }))
        .await
        .ok()
        .json();

    let patched = client.patch(&format!("/child-vitals/{}", id(&created)), json!({ "heart": "murmur" })).await;
    let fetched = client.get(&format!("/vitals/{vitals_id}/child-vitals")).await;

    assert_eq!(patched.status, StatusCode::OK);
    assert_eq!(fetched.status, StatusCode::OK);
    assert_eq!(fetched.json()["heart"], "murmur");
}

#[tokio::test]
async fn child_vitals_can_be_deleted() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;
    let vitals_id = create_vitals(&mut client, visit_id).await;
    let created = client
        .post("/child-vitals", json!({ "vitalsId": vitals_id, "heart": "normal", "pallor": false }))
        .await
        .ok()
        .json();

    let deleted = client.delete(&format!("/child-vitals/{}", id(&created))).await;

    assert_eq!(deleted.status, StatusCode::NO_CONTENT);
}

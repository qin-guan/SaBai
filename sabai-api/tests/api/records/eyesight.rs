use reqwest::StatusCode;
use serde_json::json;

use crate::support::*;

#[tokio::test]
async fn degree_and_pinhole_are_normalized_when_recorded() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;

    let created = client
        .post("/eyesight", json!({ "visitId": visit_id, "leftEyeDegree": "6/9+2", "leftEyePinhole": "ni" }))
        .await;
    let fetched = client.get(&format!("/visits/{visit_id}/eyesight")).await;
    let body = fetched.json();

    assert_eq!(created.status, StatusCode::CREATED);
    assert_eq!(fetched.status, StatusCode::OK);
    assert_eq!(body["leftEyeDegree"], "6/9+2");
    assert_eq!(body["leftEyePinhole"], "NI");
}

#[tokio::test]
async fn invalid_degree_is_rejected() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;

    let response = client.post("/eyesight", json!({ "visitId": visit_id, "leftEyeDegree": "20/20" })).await;

    assert_eq!(response.status, StatusCode::BAD_REQUEST);
}

#[tokio::test]
async fn invalid_pinhole_is_rejected() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;

    let response = client.post("/eyesight", json!({ "visitId": visit_id, "leftEyePinhole": "nope" })).await;

    assert_eq!(response.status, StatusCode::BAD_REQUEST);
}

#[tokio::test]
async fn second_eyesight_for_a_visit_is_a_conflict() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;
    client.post("/eyesight", json!({ "visitId": visit_id, "leftEyeDegree": "6/6" })).await.ok();

    let again = client.post("/eyesight", json!({ "visitId": visit_id, "leftEyeDegree": "6/9" })).await;

    assert_eq!(again.status, StatusCode::CONFLICT);
}

#[tokio::test]
async fn unknown_eyesight_is_not_found() {
    let mut client = Client::admin().await;

    let response = client.get("/eyesight/99999999").await;

    assert_eq!(response.status, StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn deleted_eyesight_is_gone_from_its_visit() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;
    let created = client.post("/eyesight", json!({ "visitId": visit_id, "leftEyeDegree": "CF" })).await.ok().json();

    let deleted = client.delete(&format!("/eyesight/{}", id(&created))).await;
    let fetched = client.get(&format!("/visits/{visit_id}/eyesight")).await;

    assert_eq!(deleted.status, StatusCode::NO_CONTENT);
    assert_eq!(fetched.status, StatusCode::NOT_FOUND);
}

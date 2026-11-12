use reqwest::StatusCode;
use serde_json::json;

use crate::support::*;

#[tokio::test]
async fn recorded_glasses_are_returned_for_the_visit_with_degree_and_notes() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;

    let created =
        client.post("/glasses", json!({ "visitId": visit_id, "leftGlassesDegree": "6/12", "notes": "trial" })).await;
    let fetched = client.get(&format!("/glasses?visitId={visit_id}")).await;
    let body = fetched.json();

    assert_eq!(created.status, StatusCode::CREATED);
    assert_eq!(fetched.status, StatusCode::OK);
    assert_eq!(body["leftGlassesDegree"], "6/12");
    assert_eq!(body["notes"], "trial");
}

#[tokio::test]
async fn glasses_for_an_unknown_visit_are_not_found() {
    let mut client = Client::admin().await;

    let response =
        client.post("/glasses", json!({ "visitId": 99999999, "leftGlassesDegree": "6/12", "notes": "trial" })).await;

    assert_eq!(response.status, StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn visit_without_glasses_has_none_to_return() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;

    let response = client.get(&format!("/glasses?visitId={visit_id}")).await;

    assert_eq!(response.status, StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn empty_notes_clear_the_notes() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;
    let created = client
        .post("/glasses", json!({ "visitId": visit_id, "leftGlassesDegree": "6/12", "notes": "trial" }))
        .await
        .ok()
        .json();

    let patched = client.patch(&format!("/glasses/{}", id(&created)), json!({ "notes": "" })).await;
    let notes = &patched.json()["notes"];

    assert_eq!(patched.status, StatusCode::OK);
    assert!(notes.is_null() || notes == "");
}

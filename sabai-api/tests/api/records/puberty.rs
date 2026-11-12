use reqwest::StatusCode;
use serde_json::json;

use super::create_vitals;
use crate::support::*;

#[tokio::test]
async fn created_puberty_assessment_is_returned_for_its_vitals() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;
    let vitals_id = create_vitals(&mut client, visit_id).await;

    let created = client.post("/puberty", json!({ "vitalsId": vitals_id, "pubarche": true, "pubarcheAge": 12 })).await;
    let fetched = client.get(&format!("/vitals/{vitals_id}/puberty")).await;

    assert_eq!(created.status, StatusCode::CREATED);
    assert_eq!(fetched.status, StatusCode::OK);
    assert_eq!(fetched.json()["pubarcheAge"], 12);
}

#[tokio::test]
async fn puberty_assessment_for_unknown_vitals_is_not_found() {
    let mut client = Client::admin().await;

    let response = client.post("/puberty", json!({ "vitalsId": 99999999, "pubarche": true, "pubarcheAge": 12 })).await;

    assert_eq!(response.status, StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn second_puberty_assessment_for_the_same_vitals_is_a_conflict() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;
    let vitals_id = create_vitals(&mut client, visit_id).await;
    client.post("/puberty", json!({ "vitalsId": vitals_id, "pubarche": true, "pubarcheAge": 12 })).await.ok();

    let again = client.post("/puberty", json!({ "vitalsId": vitals_id, "pubarche": true, "pubarcheAge": 13 })).await;

    assert_eq!(again.status, StatusCode::CONFLICT);
}

#[tokio::test]
async fn updating_with_an_age_for_a_milestone_not_reached_is_rejected() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;
    let vitals_id = create_vitals(&mut client, visit_id).await;
    let created = client
        .post("/puberty", json!({ "vitalsId": vitals_id, "pubarche": true, "pubarcheAge": 12 }))
        .await
        .ok()
        .json();

    let patched =
        client.patch(&format!("/puberty/{}", id(&created)), json!({ "pubarche": false, "pubarcheAge": 10 })).await;

    assert_eq!(patched.status, StatusCode::BAD_REQUEST);
}

#[tokio::test]
async fn puberty_assessment_can_be_deleted() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;
    let vitals_id = create_vitals(&mut client, visit_id).await;
    let created = client
        .post("/puberty", json!({ "vitalsId": vitals_id, "pubarche": true, "pubarcheAge": 12 }))
        .await
        .ok()
        .json();

    let deleted = client.delete(&format!("/puberty/{}", id(&created))).await;

    assert_eq!(deleted.status, StatusCode::NO_CONTENT);
}

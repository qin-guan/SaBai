use reqwest::StatusCode;
use serde_json::json;

use crate::support::*;

#[tokio::test]
async fn vision_contains_the_prescribed_degree_after_glasses_are_recorded() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;
    let degree = "6/12";
    client.post("/glasses", json!({ "visitId": visit_id, "leftGlassesDegree": degree, "notes": "trial" })).await.ok();

    let vision = client.get(&format!("/vision?visitId={visit_id}")).await;

    assert_eq!(vision.status, StatusCode::OK);
    assert!(vision.text().contains(degree));
}

#[tokio::test]
async fn vision_without_a_visit_id_is_rejected() {
    let mut client = Client::admin().await;

    let response = client.get("/vision").await;

    assert_eq!(response.status, StatusCode::BAD_REQUEST);
}

#[tokio::test]
async fn vision_with_visit_id_zero_is_rejected() {
    let mut client = Client::admin().await;

    let response = client.get("/vision?visitId=0").await;

    assert_eq!(response.status, StatusCode::BAD_REQUEST);
}

#[tokio::test]
async fn vision_for_an_unknown_visit_is_not_found() {
    let mut client = Client::admin().await;

    let response = client.get("/vision?visitId=99999999").await;

    assert_eq!(response.status, StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn vision_signed_out_is_unauthorized() {
    let mut client = Client::anonymous();

    let response = client.get("/vision?visitId=1").await;

    assert_eq!(response.status, StatusCode::UNAUTHORIZED);
}

use reqwest::StatusCode;
use serde_json::json;

use super::*;

async fn register_patient(client: &mut Client) -> (i64, i64) {
    register(client, &format!("Consult {}", suffix())).await
}

#[tokio::test]
async fn consult_for_a_missing_visit_is_not_found() {
    let mut client = Client::admin().await;
    let details = format!("missing visit {}", suffix());

    client
        .post("/consults", json!({ "visitId": 99999999, "diagnoses": [{ "details": details, "category": "Respiratory" }] }))
        .await
        .expect(StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn consult_with_an_empty_diagnosis_list_is_rejected() {
    let mut client = Client::admin().await;
    let (_, visit_id) = register_patient(&mut client).await;

    client.post("/consults", json!({ "visitId": visit_id, "diagnoses": [] })).await.expect(StatusCode::BAD_REQUEST);
}

#[tokio::test]
async fn missing_consult_is_not_found() {
    let mut client = Client::admin().await;

    let response = client.get("/consults/99999999").await.expect(StatusCode::NOT_FOUND);
    assert_eq!(
        response.json(),
        json!({ "title": "Not Found", "status": 404, "detail": "Consult was not found.", "instance": "/consults/99999999" })
    );
}

#[tokio::test]
async fn updating_a_missing_consult_is_not_found() {
    let mut client = Client::admin().await;

    let response = client.patch("/consults/99999999", json!({ "consultation": "absent" })).await.expect(StatusCode::NOT_FOUND);
    assert_eq!(response.detail(), "Consult was not found.");
}

#[tokio::test]
async fn deleting_a_missing_consult_is_not_found() {
    let mut client = Client::admin().await;

    let response = client.delete("/consults/99999999").await.expect(StatusCode::NOT_FOUND);
    assert_eq!(response.detail(), "Consult was not found.");
}

#[tokio::test]
async fn consult_with_a_referral_cannot_be_deleted() {
    let mut client = Client::admin().await;
    let suffix = suffix();
    let (_, visit_id) = register_patient(&mut client).await;
    let consult_id = consult_with(&mut client, visit_id, &format!("blocked {suffix}")).await;
    client.post("/referrals", json!({ "consultId": consult_id, "referredFor": format!("hold {suffix}") })).await.ok();

    let response = client.delete(&format!("/consults/{consult_id}")).await.expect(StatusCode::CONFLICT);
    assert_eq!(response.detail(), "Cannot delete a consult that still has referrals.");
}

#[tokio::test]
async fn consult_pdf_needs_a_session() {
    let mut client = Client::admin().await;
    let (_, visit_id) = register_patient(&mut client).await;
    let consult_id = consult_with(&mut client, visit_id, &format!("pdf {}", suffix())).await;
    let mut anonymous = Client::anonymous();

    let response = anonymous.get(&format!("/consults/{consult_id}/pdf")).await.expect(StatusCode::UNAUTHORIZED);
    assert!(response.bytes.is_empty());
}

#[tokio::test]
async fn consult_is_returned_with_doctor_nickname_and_diagnosis() {
    let mut client = Client::admin().await;
    let details = format!("dry cough {}", suffix());
    let (_, visit_id) = register_patient(&mut client).await;
    let consult_id = consult_with(&mut client, visit_id, &details).await;

    let body = client.get(&format!("/consults/{consult_id}")).await.expect(StatusCode::OK).json();

    assert_eq!(body["doctorNickname"], "Admin");
    assert!(body["diagnoses"].as_array().unwrap().iter().any(|diagnosis| diagnosis["details"] == details.as_str()));
    assert_eq!(body["visitId"], visit_id);
}

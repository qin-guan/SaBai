use reqwest::StatusCode;
use serde_json::json;

use crate::support::*;

#[tokio::test]
async fn consult_is_recorded_with_its_diagnoses_by_the_signed_in_doctor() {
    let mut doctor = Client::signed_in(DOCTOR).await;
    let me = doctor.who_am_i().await;
    let (_, visit_id) = patient_with_visit(&mut doctor).await;

    let created = doctor
        .post(
            "/consults",
            json!({
                "visitId": visit_id,
                "consultation": "exam",
                "diagnoses": [{ "details": " cough ", "category": "Respiratory" }, { "details": "rash", "category": "Ear Nose Throat" }],
            }),
        )
        .await
        .expect(StatusCode::CREATED);
    let consult = created.json();
    assert_eq!(created.header("location"), Some(format!("/consults/{}", id(&consult)).as_str()));
    assert_eq!(consult["doctorId"], me["id"]);
    assert_eq!(consult["doctorNickname"], "Doctor");
    assert_eq!(consult["diagnoses"][0]["details"], "cough");
    assert_eq!(consult["diagnoses"][1]["category"], "Ear Nose Throat");
    assert_eq!(consult["diagnoses"][0]["consultId"], consult["id"]);
}

#[tokio::test]
async fn consult_needs_a_visit_and_a_diagnosis() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;

    let no_diagnosis = client.post("/consults", json!({ "visitId": visit_id })).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(no_diagnosis.errors(), [("diagnoses".to_owned(), "'diagnoses' must not be empty.".to_owned())]);

    let blank = client
        .post("/consults", json!({ "visitId": visit_id, "diagnoses": [{ "details": " " }] }))
        .await
        .expect(StatusCode::BAD_REQUEST);
    assert_eq!(blank.errors(), [("diagnoses[0].details".to_owned(), "'details' must not be empty.".to_owned())]);

    let no_visit = client
        .post("/consults", json!({ "visitId": 99999999, "diagnoses": [{ "details": "x" }] }))
        .await
        .expect(StatusCode::NOT_FOUND);
    assert_eq!(no_visit.detail(), "Visit was not found.");
}

use reqwest::StatusCode;
use sabai_api::pdf::{ConsultPdfContent, ConsultPdfDiagnosis, consult_report};
use serde_json::{Value, json};

use super::*;

/// A village and a patient with contact and identification numbers; returns the visit id.
async fn create_visit(client: &mut Client) -> i64 {
    let suffix = suffix();
    let village = client
        .post("/village-codes", json!({ "code": format!("V{suffix}"), "name": format!("Village {suffix}"), "colorHex": "#123456" }))
        .await
        .ok()
        .json();
    let patient = client
        .post(
            "/patients",
            json!({
                "name": format!("Patient {suffix}"),
                "gender": "female",
                "dateOfBirth": "1990-05-01T00:00:00+00:00",
                "drugAllergy": "None",
                "villageCodeId": id(&village),
                "contactNo": suffix,
                "identificationNumber": suffix,
                "hasPoorCard": false,
                "hasBS2Card": false,
                "hasSabaiCard": false,
                "toGetReport": false,
            }),
        )
        .await
        .ok()
        .json();
    patient["visitId"].as_i64().unwrap()
}

/// A consult with a past medical history and one diagnosis; returns its id.
async fn create_consult(client: &mut Client, visit_id: i64) -> i64 {
    let consult = client
        .post(
            "/consults",
            json!({
                "visitId": visit_id,
                "pastMedicalHistory": "asthma",
                "consultation": "initial note",
                "diagnoses": [{ "details": "Wheeze", "category": "Respiratory" }],
            }),
        )
        .await
        .ok()
        .json();
    id(&consult)
}

#[tokio::test]
async fn consult_without_a_diagnosis_is_rejected() {
    let mut client = Client::admin().await;
    let visit_id = create_visit(&mut client).await;

    client.post("/consults", json!({ "visitId": visit_id, "diagnoses": [] })).await.expect(StatusCode::BAD_REQUEST);
}

#[tokio::test]
async fn created_consult_is_listed_for_its_visit() {
    let mut client = Client::admin().await;
    let visit_id = create_visit(&mut client).await;

    let created = client
        .post(
            "/consults",
            json!({
                "visitId": visit_id,
                "consultation": "cough for 3 days",
                "diagnoses": [{ "details": "Acute cough", "category": "Respiratory" }],
            }),
        )
        .await
        .expect(StatusCode::CREATED);
    let consult = created.json();
    let listed = client.get(&format!("/visits/{visit_id}/consults")).await.ok().json();

    let matches: Vec<&Value> = listed.as_array().unwrap().iter().filter(|item| item["id"] == consult["id"]).collect();
    assert_eq!(matches.len(), 1);
    assert_eq!(matches[0]["consultation"], "cough for 3 days");
    assert!(matches[0]["diagnoses"].as_array().unwrap().iter().any(|diagnosis| diagnosis["details"] == "Acute cough"));
    assert_eq!(matches[0]["doctorNickname"], "Admin");
    assert_eq!(matches[0], &consult);
}

#[tokio::test]
async fn consults_of_a_visit_are_listed_newest_first_with_their_own_diagnoses() {
    let mut admin = Client::admin().await;
    let mut doctor = Client::signed_in(DOCTOR).await;
    let visit_id = create_visit(&mut admin).await;
    let first = consult_with(&mut admin, visit_id, "first").await;
    let second = doctor
        .post("/consults", json!({ "visitId": visit_id, "diagnoses": [{ "details": "b" }, { "details": "a", "category": "Eye" }] }))
        .await
        .ok()
        .json();
    let third = consult_with(&mut admin, visit_id, "third").await;
    // The oldest by date, although it has the highest id.
    backdate_consult(third, 2).await;

    let listed = admin.get(&format!("/visits/{visit_id}/consults")).await.expect(StatusCode::OK).json();

    let ids: Vec<i64> = listed.as_array().unwrap().iter().map(id).collect();
    assert_eq!(ids, [id(&second), first, third]);
    assert_eq!(listed[0]["doctorNickname"], "Doctor");
    assert_eq!(listed[0]["diagnoses"], second["diagnoses"]);
    assert_eq!(listed[0]["diagnoses"][0]["details"], "b");
    assert_eq!(listed[0]["diagnoses"][1]["category"], "Eye");
    assert_eq!(listed[1]["diagnoses"].as_array().unwrap().len(), 1);
    assert_eq!(listed[1]["diagnoses"][0]["details"], "first");
    assert_eq!(listed[2]["diagnoses"][0]["details"], "third");

    let unknown = admin.get("/visits/99999999/consults").await.expect(StatusCode::NOT_FOUND);
    assert_eq!(unknown.detail(), "Visit was not found.");
}

#[tokio::test]
async fn new_consult_text_can_be_updated() {
    let mut client = Client::admin().await;
    let visit_id = create_visit(&mut client).await;
    let consult_id = create_consult(&mut client, visit_id).await;

    let patched = client
        .patch(&format!("/consults/{consult_id}"), json!({ "consultation": "updated consultation" }))
        .await
        .expect(StatusCode::OK)
        .json();

    assert_eq!(patched["consultation"], "updated consultation");
    assert_eq!(patched["pastMedicalHistory"], "asthma");
}

#[tokio::test]
async fn updating_a_consult_changes_only_the_given_text_and_is_logged() {
    let mut doctor = Client::signed_in(DOCTOR).await;
    let mut admin = Client::admin().await;
    let visit_id = create_visit(&mut doctor).await;
    let consult_id = create_consult(&mut doctor, visit_id).await;
    let path = format!("/consults/{consult_id}");
    let before = doctor.get(&path).await.ok().json();

    // Null and absent fields are left alone; an empty text is a value.
    let patched = admin
        .patch(&path, json!({ "id": 999999, "remarks": "", "treatmentPlan": "rest", "referredFor": null, "consultation": null }))
        .await
        .expect(StatusCode::OK)
        .json();

    let mut expected = before.clone();
    expected["remarks"] = json!("");
    expected["treatmentPlan"] = json!("rest");
    assert_eq!(patched, expected);
    assert_eq!(patched["doctorNickname"], "Doctor", "the consult stays with the doctor who wrote it");
    assert_eq!(doctor.get(&path).await.ok().json(), expected);

    let unchanged = admin.patch(&path, json!({})).await.expect(StatusCode::OK).json();
    assert_eq!(unchanged, expected);

    let entries = audit_entries(&mut admin, "Consult", consult_id).await;
    let actions: Vec<&str> = entries.iter().map(|entry| entry["action"].as_str().unwrap()).collect();
    assert_eq!(actions, ["Updated", "Created"], "an update that changes nothing is not logged");
    assert_eq!(changed_fields(&entries[0]), ["Remarks", "TreatmentPlan"]);
    assert_eq!(entries[0]["userName"], "admin");
}

#[tokio::test]
async fn consult_cannot_be_edited_after_24_hours() {
    let mut client = Client::admin().await;
    let visit_id = create_visit(&mut client).await;
    let recent = create_consult(&mut client, visit_id).await;
    let old = create_consult(&mut client, visit_id).await;
    backdate_consult(recent, 23).await;
    backdate_consult(old, 25).await;

    client.patch(&format!("/consults/{recent}"), json!({ "remarks": "still open" })).await.expect(StatusCode::OK);
    let path = format!("/consults/{old}");
    let locked = client.patch(&path, json!({ "remarks": "too late" })).await.expect(StatusCode::METHOD_NOT_ALLOWED);

    assert_eq!(locked.header("content-type"), Some("application/json; charset=utf-8"));
    assert_eq!(
        locked.json(),
        json!({ "title": "Locked", "status": 405, "detail": "Cannot edit consult after 24 hours", "instance": path })
    );
    assert_eq!(client.get(&path).await.ok().json()["remarks"], Value::Null);
}

#[tokio::test]
async fn consult_can_be_deleted() {
    let mut client = Client::admin().await;
    let visit_id = create_visit(&mut client).await;
    let consult_id = create_consult(&mut client, visit_id).await;

    let deleted = client.delete(&format!("/consults/{consult_id}")).await.expect(StatusCode::NO_CONTENT);
    assert!(deleted.bytes.is_empty());
}

#[tokio::test]
async fn deleting_a_consult_removes_its_diagnoses_and_logs_the_consult_only() {
    let mut client = Client::admin().await;
    let visit_id = create_visit(&mut client).await;
    let consult_id = create_consult(&mut client, visit_id).await;
    let diagnosis_id = id(&client.get(&format!("/diagnoses?consultId={consult_id}")).await.ok().json()[0]);

    client.delete(&format!("/consults/{consult_id}")).await.expect(StatusCode::NO_CONTENT);

    client.get(&format!("/consults/{consult_id}")).await.expect(StatusCode::NOT_FOUND);
    client.get(&format!("/diagnoses/{diagnosis_id}")).await.expect(StatusCode::NOT_FOUND);
    let consult_log = audit_entries(&mut client, "Consult", consult_id).await;
    assert_eq!(consult_log[0]["action"], "Deleted");
    assert_eq!(consult_log[0]["request"], format!("DELETE /consults/{consult_id}"));
    assert!(changed_fields(&consult_log[0]).contains(&"PastMedicalHistory"));
    let diagnosis_log = audit_entries(&mut client, "Diagnosis", diagnosis_id).await;
    let actions: Vec<&str> = diagnosis_log.iter().map(|entry| entry["action"].as_str().unwrap()).collect();
    assert_eq!(actions, ["Created"], "diagnoses removed with their consult are not logged");
}

#[tokio::test]
async fn consult_with_prescriptions_cannot_be_deleted() {
    let mut client = Client::admin().await;
    let visit_id = create_visit(&mut client).await;
    let brand = medication_brand(&format!("Brand {}", suffix())).await;
    let consult = client
        .post(
            "/consults",
            json!({
                "visitId": visit_id,
                "diagnoses": [{ "details": "x" }],
                "orders": [{ "medicationBrandId": brand, "quantity": 1 }],
            }),
        )
        .await
        .ok()
        .json();

    let path = format!("/consults/{}", id(&consult));
    let blocked = client.delete(&path).await.expect(StatusCode::CONFLICT);

    assert_eq!(blocked.detail(), "Cannot delete a consult that still has orders.");
    client.get(&path).await.expect(StatusCode::OK);
}

#[tokio::test]
async fn new_referral_starts_in_the_new_state() {
    let mut client = Client::admin().await;
    let visit_id = create_visit(&mut client).await;
    let consult_id = create_consult(&mut client, visit_id).await;

    let referral = client
        .post("/referrals", json!({ "consultId": consult_id, "referredFor": "Cardiology", "referralNotes": "soon" }))
        .await
        .expect(StatusCode::CREATED)
        .json();

    assert_eq!(referral["referralState"], "New");
    assert_eq!(referral["referralOutcome"], Value::Null);
}

#[tokio::test]
async fn referral_state_change_keeps_an_outcome_that_is_not_sent() {
    let mut client = Client::admin().await;
    let visit_id = create_visit(&mut client).await;
    let consult_id = create_consult(&mut client, visit_id).await;
    let referral = client.post("/referrals", json!({ "consultId": consult_id, "referredFor": "ENT" })).await.ok().json();
    let path = format!("/referrals/{}/state", id(&referral));

    client.patch(&path, json!({ "referralState": "Seen", "referralOutcome": "booked" })).await.expect(StatusCode::OK);
    let completed = client.patch(&path, json!({ "referralState": "Completed" })).await.expect(StatusCode::OK).json();

    assert_eq!(completed["referralState"], "Completed");
    assert_eq!(completed["referralOutcome"], "booked");
}

#[tokio::test]
async fn patient_records_need_a_visit_id() {
    let mut client = Client::admin().await;

    let response = client.get("/patient-records").await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(response.detail(), "visitId must be a positive integer.");
}

#[tokio::test]
async fn patient_consult_needs_a_visit_id() {
    let mut client = Client::admin().await;

    let response = client.get("/patient-consult").await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(response.detail(), "visitId must be a positive integer.");
}

#[tokio::test]
async fn consult_pdf_is_served_as_a_pdf() {
    let mut client = Client::admin().await;
    let visit_id = create_visit(&mut client).await;
    let consult_id = create_consult(&mut client, visit_id).await;

    let pdf = client.get(&format!("/consults/{consult_id}/pdf")).await.expect(StatusCode::OK);

    assert_eq!(pdf.header("content-type"), Some("application/pdf"));
    assert!(pdf.bytes.starts_with(b"%PDF"));
}

#[test]
fn consult_pdf_with_khmer_text_is_a_pdf() {
    let bytes = consult_report(&ConsultPdfContent {
        patient_display_id: "PC0012".into(),
        patient_name: "សុខ តារា (Sok Dara)".into(),
        doctor_nickname: "វេជ្ជបណ្ឌិត ហេង សុភា".into(),
        past_medical_history: Some("ជំងឺហឺត".into()),
        consultation: Some("ក្អករយៈពេល ៣ ថ្ងៃ និងគ្រុនក្តៅ".into()),
        diagnoses: vec![ConsultPdfDiagnosis { category: Some("Respiratory".into()), details: "ក្អកស្រួចស្រាវ".into() }],
        treatment_plan: Some("សម្រាក និងផឹកទឹកឱ្យបានច្រើន".into()),
        referred_for: Some("ផ្នែកធ្មេញ".into()),
        referral_notes: Some("ពិនិត្យបន្ត".into()),
        remarks: Some("គ្មាន".into()),
    })
    .unwrap();

    assert!(bytes.starts_with(b"%PDF"));
}

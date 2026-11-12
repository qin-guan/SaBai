use reqwest::StatusCode;
use serde_json::json;

use crate::support::*;

/// A stored image belonging to the patient, as `POST /files` would leave it. The row is
/// written directly: the dental consultation only links to it and never reads its bytes.
async fn stored_image(patient_id: i64) -> i64 {
    let name = format!("dental-{}.png", suffix());
    let (file_id,): (i32,) = sqlx::query_as(
        r#"INSERT INTO "Files" ("PatientId", "FileName", "StoredFileName", "Description", "SizeBytes", "IsDeleted", "CreatedAt")
           VALUES ($1, $2, $3, 'dental image', 8, false, now()) RETURNING "Id""#,
    )
    .bind(patient_id as i32)
    .bind(&name)
    .bind(format!("{}-{name}", suffix()))
    .fetch_one(&db().await)
    .await
    .expect("inserting the file row");
    i64::from(file_id)
}

#[tokio::test]
async fn saving_creates_the_consultation_and_returns_it() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;

    let saved = client
        .put(
            &format!("/visits/{visit_id}/dental"),
            json!({
                "previousDentalVisit": "Unknown",
                "toothache": true,
                "brushingFrequency": "ThreeOrMore",
                "chiefComplaint": "  Pain on lower left  ",
                "facialSymmetry": "Asymmetrical",
                "lymphadenopathy": "NotAssessed",
                "plaque": "Heavy",
                "caries": "NonCavitated",
                "malocclusion": "Suspected",
                "diagnoses": ["Dental caries", "  ", "Gingivitis"],
                "fluorideDone": true,
                "fluorideDate": "2026-11-02",
                "fluorideOperator": "Dr Tan",
                "fluorideToothNumbers": "36, 46",
                "referredFor": "Restorative",
                "referralPriority": "Early",
            }),
        )
        .await;
    let fetched = client.get(&format!("/visits/{visit_id}/dental")).await;
    let body = fetched.json();

    assert_eq!(saved.status, StatusCode::OK);
    assert_eq!(fetched.status, StatusCode::OK);
    assert_eq!(body["previousDentalVisit"], "Unknown");
    assert_eq!(body["toothache"], true);
    assert_eq!(body["brushingFrequency"], "ThreeOrMore");
    assert_eq!(body["chiefComplaint"], "Pain on lower left");
    assert_eq!(body["caries"], "NonCavitated");
    assert_eq!(body["diagnoses"].as_array().unwrap().len(), 2);
    assert_eq!(body["fluorideDate"], "2026-11-02");
    assert_eq!(body["referralPriority"], "Early");
}

#[tokio::test]
async fn saving_again_replaces_the_consultation_and_clears_omitted_fields() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;
    let path = format!("/visits/{visit_id}/dental");
    let first = client.put(&path, json!({ "oralHygiene": "Poor", "remarks": "First" })).await.ok().json();

    let second = client.put(&path, json!({ "oralHygiene": "Good" })).await;
    let body = second.json();

    assert_eq!(second.status, StatusCode::OK);
    assert_eq!(body["id"], first["id"]);
    assert_eq!(body["oralHygiene"], "Good");
    assert!(body["remarks"].is_null());
}

#[tokio::test]
async fn unknown_option_is_rejected() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;

    let response = client.put(&format!("/visits/{visit_id}/dental"), json!({ "plaque": "Extreme" })).await;

    assert_eq!(response.status, StatusCode::BAD_REQUEST);
}

#[tokio::test]
async fn saving_for_a_missing_visit_is_not_found() {
    let mut client = Client::admin().await;

    let response = client.put("/visits/999999999/dental", json!({ "oralHygiene": "Good" })).await;

    assert_eq!(response.status, StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn visit_without_a_consultation_has_none_to_return() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;

    let response = client.get(&format!("/visits/{visit_id}/dental")).await;

    assert_eq!(response.status, StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn image_file_is_linked_and_omitting_it_clears_the_link() {
    let mut client = Client::admin().await;
    let (patient_id, visit_id) = patient_with_visit(&mut client).await;
    let path = format!("/visits/{visit_id}/dental");
    let file_id = stored_image(patient_id).await;

    let linked = client.put(&path, json!({ "chiefComplaint": "Pain", "imageFileId": file_id })).await;
    let cleared = client.put(&path, json!({ "chiefComplaint": "Pain" })).await;

    assert_eq!(linked.status, StatusCode::OK);
    assert_eq!(linked.json()["imageFileId"], file_id);
    assert_eq!(cleared.status, StatusCode::OK);
    assert!(cleared.json()["imageFileId"].is_null());
}

#[tokio::test]
async fn unknown_image_file_is_rejected() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;

    let response = client.put(&format!("/visits/{visit_id}/dental"), json!({ "imageFileId": 99999999 })).await;

    assert_eq!(response.status, StatusCode::BAD_REQUEST);
}

#[tokio::test]
async fn image_file_of_another_patient_is_rejected() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;
    let (other_patient_id, _) = patient_with_visit(&mut client).await;
    let file_id = stored_image(other_patient_id).await;

    let response = client.put(&format!("/visits/{visit_id}/dental"), json!({ "imageFileId": file_id })).await;

    assert_eq!(response.status, StatusCode::BAD_REQUEST);
}

#[tokio::test]
async fn soft_deleted_image_file_is_rejected() {
    let mut client = Client::admin().await;
    let (patient_id, visit_id) = patient_with_visit(&mut client).await;
    let file_id = stored_image(patient_id).await;
    sqlx::query(r#"UPDATE "Files" SET "IsDeleted" = true WHERE "Id" = $1"#)
        .bind(file_id as i32)
        .execute(&db().await)
        .await
        .expect("soft-deleting the file row");

    let response = client.put(&format!("/visits/{visit_id}/dental"), json!({ "imageFileId": file_id })).await;

    assert_eq!(response.status, StatusCode::BAD_REQUEST);
}

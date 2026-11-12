use reqwest::StatusCode;
use serde_json::json;

use crate::support::*;

struct Seed {
    patient_id: i64,
    visit_id: i64,
    village_code: String,
    patient_name: String,
}

/// A new village with one registered boy and his open visit.
async fn seed_visit(client: &mut Client) -> Seed {
    let village_code = village_code();
    let patient_name = format!("Patient {}", suffix());
    let village = create_village_named(client, &village_code, &format!("Village {}", suffix())).await;
    let patient = client
        .post(
            "/patients",
            json!({
                "name": patient_name,
                "gender": "male",
                "dateOfBirth": "2014-03-02T00:00:00+00:00",
                "drugAllergy": "NKDA",
                "hasPoorCard": false,
                "hasBS2Card": false,
                "hasSabaiCard": false,
                "villageCodeId": id(&village),
            }),
        )
        .await
        .expect(StatusCode::CREATED)
        .json();
    Seed { patient_id: id(&patient), visit_id: patient["visitId"].as_i64().unwrap(), village_code, patient_name }
}

#[tokio::test]
async fn second_vitals_for_a_visit_is_a_conflict() {
    let mut client = Client::admin().await;
    let seed = seed_visit(&mut client).await;

    let created = client.post("/vitals", json!({ "visitId": seed.visit_id, "height": 160, "weight": 50 })).await;
    let again = client.post("/vitals", json!({ "visitId": seed.visit_id, "height": 161 })).await;
    let body = created.json();

    assert_eq!(created.status, StatusCode::CREATED);
    assert!(created.header("location").is_some());
    assert_eq!(body["visitId"], seed.visit_id);
    assert_eq!(body["height"].as_f64(), Some(160.0));
    assert_eq!(again.status, StatusCode::CONFLICT);
}

#[tokio::test]
async fn vitals_are_returned_by_visit_and_listed_for_the_patient() {
    let mut client = Client::admin().await;
    let seed = seed_visit(&mut client).await;
    let saved =
        client.post("/vitals", json!({ "visitId": seed.visit_id, "heartRate": 72, "urineTest": "clear" })).await.json();

    let by_visit = client.get(&format!("/visits/{}/vitals", seed.visit_id)).await;
    let body = by_visit.json();
    let listed = client.get(&format!("/patients/{}/vitals", seed.patient_id)).await;
    let list = listed.json();

    assert_eq!(by_visit.status, StatusCode::OK);
    assert_eq!(body["id"], saved["id"]);
    assert_eq!(body["visitId"], seed.visit_id);
    assert_eq!(body["heartRate"], 72);
    assert_eq!(body["urineTest"], "clear");
    assert_eq!(listed.status, StatusCode::OK);
    assert_eq!(list.as_array().unwrap().len(), 1);
    assert_eq!(list[0]["id"], saved["id"]);
}

#[tokio::test]
async fn updating_vitals_changes_only_the_provided_fields() {
    let mut client = Client::admin().await;
    let seed = seed_visit(&mut client).await;
    let saved = client.post("/vitals", json!({ "visitId": seed.visit_id, "height": 150, "weight": 40 })).await.json();

    let patched = client.patch(&format!("/vitals/{}", id(&saved)), json!({ "height": 151 })).await;
    let body = patched.json();

    assert_eq!(patched.status, StatusCode::OK);
    assert_eq!(body["height"].as_f64(), Some(151.0));
    assert_eq!(body["weight"].as_f64(), Some(40.0));
    assert_eq!(body["visitId"], seed.visit_id);
}

#[tokio::test]
async fn deleted_vitals_are_gone() {
    let mut client = Client::admin().await;
    let seed = seed_visit(&mut client).await;
    let saved = client.post("/vitals", json!({ "visitId": seed.visit_id, "temperature": 36.6 })).await.json();

    let deleted = client.delete(&format!("/vitals/{}", id(&saved))).await;
    let fetched = client.get(&format!("/vitals/{}", id(&saved))).await;

    assert_eq!(deleted.status, StatusCode::NO_CONTENT);
    assert_eq!(fetched.status, StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn eyesight_with_an_invalid_acuity_is_rejected() {
    let mut client = Client::admin().await;
    let seed = seed_visit(&mut client).await;

    let response = client.post("/eyesight", json!({ "visitId": seed.visit_id, "leftEyeDegree": "20/20" })).await;

    assert_eq!(response.status, StatusCode::BAD_REQUEST);
    assert!(response.text().contains("VA notation"));
}

#[tokio::test]
async fn eyesight_acuity_is_stored_uppercased() {
    let mut client = Client::admin().await;
    let seed = seed_visit(&mut client).await;

    let created = client
        .post(
            "/eyesight",
            json!({ "visitId": seed.visit_id, "leftEyeDegree": "6/9+2", "rightEyeDegree": "cf", "leftEyePinhole": "ni" }),
        )
        .await;
    let fetched = client.get(&format!("/visits/{}/eyesight", seed.visit_id)).await;
    let body = fetched.json();

    assert_eq!(created.status, StatusCode::CREATED);
    assert_eq!(fetched.status, StatusCode::OK);
    assert_eq!(body["leftEyeDegree"], "6/9+2");
    assert_eq!(body["rightEyeDegree"], "CF");
    assert_eq!(body["leftEyePinhole"], "NI");
}

#[tokio::test]
async fn puberty_age_for_a_milestone_not_reached_is_rejected() {
    let mut client = Client::admin().await;
    let seed = seed_visit(&mut client).await;
    let saved = client.post("/vitals", json!({ "visitId": seed.visit_id })).await.json();

    let denied = client.post("/puberty", json!({ "vitalsId": id(&saved), "pubarche": false, "pubarcheAge": 12 })).await;
    let created = client.post("/puberty", json!({ "vitalsId": id(&saved), "pubarche": true, "pubarcheAge": 12 })).await;

    assert_eq!(denied.status, StatusCode::BAD_REQUEST);
    assert!(denied.text().contains("pubarcheAge must not be set when pubarche is false"));
    assert_eq!(created.status, StatusCode::CREATED);
}

#[tokio::test]
async fn vision_composite_returns_the_patient_and_the_glasses_recorded() {
    let mut client = Client::admin().await;
    let seed = seed_visit(&mut client).await;

    let created = client
        .post(
            "/glasses",
            json!({
                "visitId": seed.visit_id,
                "leftGlassesDegree": "-1.25",
                "rightGlassesDegree": "-0.75",
                "notes": "distance",
            }),
        )
        .await;
    let vision = client.get(&format!("/vision?visitId={}", seed.visit_id)).await;
    let body = vision.json();

    assert_eq!(created.status, StatusCode::CREATED);
    assert_eq!(vision.status, StatusCode::OK);
    assert_eq!(body["patient"]["id"], seed.patient_id);
    assert_eq!(body["patient"]["name"], seed.patient_name);
    assert_eq!(body["patient"]["displayId"], format!("{}{:04}", seed.village_code, seed.patient_id));
    assert_eq!(body["patient"]["gender"], "male");
    assert!(body["vision"].is_object());
    assert_eq!(body["vision"]["visitId"], seed.visit_id);
    assert_eq!(body["vision"]["leftGlassesDegree"], "-1.25");
    assert_eq!(body["vision"]["rightGlassesDegree"], "-0.75");
    assert_eq!(body["vision"]["notes"], "distance");
}

use reqwest::StatusCode;
use serde_json::json;

use crate::support::*;

#[tokio::test]
async fn registering_a_patient_opens_a_visit() {
    let mut client = Client::admin().await;
    let village = create_village(&mut client).await;

    let created = client
        .post(
            "/patients",
            json!({
                "name": " P ", "gender": "female", "dateOfBirth": "1990-01-01", "drugAllergy": "n",
                "villageCodeId": id(&village), "vitals": { "temperature": 36.50, "height": "170.25" },
            }),
        )
        .await
        .expect(StatusCode::CREATED);
    let patient = created.json();
    let code = village["code"].as_str().unwrap();
    let number = id(&patient);
    assert_eq!(created.header("location"), Some(format!("/patients/{number}").as_str()));
    assert_eq!(patient["name"], "P");
    assert_eq!(patient["dateOfBirth"], "1990-01-01T00:00:00+00:00");
    assert_eq!(patient["displayId"], format!("{code}{number:04}"));
    assert_eq!(patient["filterString"], format!("{code}{number:04} {code}{number}  P"));
    assert_eq!(patient["visitId"], patient["lastVisitId"]);
    assert!(patient["vitalsId"].is_i64());
    assert_eq!(patient["hasBS2Card"], false);
    assert_eq!(patient["hasPhoto"], false);
}

#[tokio::test]
async fn registration_requires_the_basics() {
    let mut client = Client::admin().await;
    let invalid = client.post("/patients", json!({ "name": "", "drugAllergy": "" })).await.expect(StatusCode::BAD_REQUEST);
    let names: Vec<_> = invalid.errors().into_iter().map(|(name, _)| name).collect();
    assert_eq!(names, ["name", "gender", "dateOfBirth", "drugAllergy", "villageCodeId"]);

    let unknown_village = client
        .post("/patients", json!({ "name": "P", "gender": "male", "dateOfBirth": "1990-01-01", "drugAllergy": "n", "villageCodeId": 99999999 }))
        .await
        .expect(StatusCode::NOT_FOUND);
    assert_eq!(unknown_village.detail(), "Village code was not found.");
}

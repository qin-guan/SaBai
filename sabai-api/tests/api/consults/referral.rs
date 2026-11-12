use reqwest::StatusCode;
use serde_json::{Value, json};

use super::*;

/// A referral for a new consult; returns its id.
async fn referral_for(client: &mut Client, patient_name: &str, referred_for: &str) -> i64 {
    let (consult_id, _) = consult_for_new_patient(client, patient_name).await;
    id(&client.post("/referrals", json!({ "consultId": consult_id, "referredFor": referred_for })).await.ok().json())
}

#[tokio::test]
async fn new_referral_is_new_and_is_returned_with_its_patient() {
    let mut client = Client::admin().await;
    let patient_name = format!("Referral {}", suffix());
    let (consult_id, _) = consult_for_new_patient(&mut client, &patient_name).await;

    let created = client.post("/referrals", json!({ "consultId": consult_id, "referredFor": "eye clinic" })).await.expect(StatusCode::CREATED);
    let referral = created.json();
    let path = format!("/referrals/{}", id(&referral));
    let listed = client.get(&format!("/consults/{consult_id}/referrals")).await.expect(StatusCode::OK).json();
    let fetched = client.get(&path).await.expect(StatusCode::OK).json();

    assert_eq!(created.header("location"), Some(path.as_str()));
    assert_eq!(referral["referralState"], "New");
    assert!(listed.as_array().unwrap().iter().any(|item| item["id"] == referral["id"]));
    assert_eq!(fetched["patientName"], patient_name.as_str());
    assert_eq!(fetched["hasPhoto"], false);
    assert!(fetched["villageCodeId"].as_i64().unwrap() > 0);
    assert!(fetched["villageCode"].is_string());
    assert!(fetched["villageColorHex"].is_string());
}

#[tokio::test]
async fn referral_views_carry_the_consult_doctor_patient_and_village() {
    let mut admin = Client::admin().await;
    let mut doctor = Client::signed_in(DOCTOR).await;
    let doctor_id = doctor.who_am_i().await["id"].clone();
    let village = create_village(&mut admin).await;
    let patient = register_patient(&mut admin, id(&village)).await;
    let consult = create_consult(&mut doctor, patient["visitId"].as_i64().unwrap()).await;

    // Opened by the admin on the doctor's consult: the doctor shown is the consult's.
    let first = admin
        .post("/referrals", json!({ "consultId": id(&consult), "referredFor": " eye clinic ", "referralNotes": " keep spaces " }))
        .await
        .expect(StatusCode::CREATED)
        .json();
    let second = admin.post("/referrals", json!({ "consultId": id(&consult), "referredFor": "dental" })).await.ok().json();

    let row = json!({
        "id": id(&first),
        "referredFor": "eye clinic",
        "referralNotes": " keep spaces ",
        "referralState": "New",
        "referralOutcome": null,
        "consultId": id(&consult),
        "createdAt": first["createdAt"],
    });
    let mut summary = row.clone();
    summary["consultDate"] = consult["date"].clone();
    summary["doctorId"] = doctor_id;
    summary["patientId"] = patient["id"].clone();
    summary["patientName"] = patient["name"].clone();
    let mut list_item = summary.clone();
    list_item["hasPhoto"] = json!(false);
    list_item["villageCodeId"] = village["id"].clone();
    list_item["villageCode"] = village["code"].clone();
    list_item["villageColorHex"] = json!("#336699");
    let mut detail = list_item.clone();
    detail["doctorNickname"] = json!("Doctor");
    summary["doctorNickname"] = json!("Doctor");

    assert_eq!(first, summary);
    assert_eq!(admin.get(&format!("/referrals/{}", id(&first))).await.ok().json(), detail);

    let of_consult = admin.get(&format!("/consults/{}/referrals", id(&consult))).await.ok().json();
    let ids: Vec<i64> = of_consult.as_array().unwrap().iter().map(id).collect();
    assert_eq!(ids, [id(&second), id(&first)], "newest first");
    assert_eq!(of_consult[1], row);

    let all = admin.get("/referrals").await.expect(StatusCode::OK).json();
    let all = all.as_array().unwrap();
    let position = |referral: &Value| all.iter().position(|item| item["id"] == referral["id"]).expect("listed");
    assert!(position(&second) < position(&first), "newest first");
    assert_eq!(all[position(&first)], list_item);

    let unknown = admin.get("/consults/99999999/referrals").await.expect(StatusCode::NOT_FOUND);
    assert_eq!(unknown.detail(), "Consult was not found.");
}

#[tokio::test]
async fn referral_for_a_missing_consult_is_not_found() {
    let mut client = Client::admin().await;

    let response = client
        .post("/referrals", json!({ "consultId": 99999999, "referredFor": format!("missing {}", suffix()) }))
        .await
        .expect(StatusCode::NOT_FOUND);
    assert_eq!(response.detail(), "Consult was not found.");
}

#[tokio::test]
async fn referral_without_a_reason_is_rejected() {
    let mut client = Client::admin().await;
    let (consult_id, _) = consult_for_new_patient(&mut client, &format!("Empty refer {}", suffix())).await;

    let response = client.post("/referrals", json!({ "consultId": consult_id, "referredFor": "" })).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(response.errors(), [("referredFor".to_owned(), "'referred For' must not be empty.".to_owned())]);

    let nothing = client.post("/referrals", json!({})).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(
        nothing.errors(),
        [
            ("consultId".to_owned(), "'consult Id' must be greater than '0'.".to_owned()),
            ("referredFor".to_owned(), "'referred For' must not be empty.".to_owned()),
        ]
    );
}

#[tokio::test]
async fn missing_referral_is_not_found() {
    let mut client = Client::admin().await;

    let response = client.get("/referrals/99999999").await.expect(StatusCode::NOT_FOUND);
    assert_eq!(response.detail(), "Referral was not found.");
}

#[tokio::test]
async fn outcome_update_keeps_the_state_and_can_be_cleared() {
    let mut client = Client::admin().await;
    let suffix = suffix();
    let referral_id = referral_for(&mut client, &format!("Outcome {suffix}"), &format!("outcome {suffix}")).await;
    client.patch(&format!("/referrals/{referral_id}/state"), json!({ "referralState": "Seen" })).await.ok();
    let path = format!("/referrals/{referral_id}/outcome");

    let set = client.patch(&path, json!({ "referralOutcome": "booked for Monday" })).await.expect(StatusCode::OK).json();
    let cleared = client.patch(&path, json!({ "referralOutcome": "" })).await.expect(StatusCode::OK).json();

    assert_eq!(set["referralState"], "Seen");
    assert_eq!(set["referralOutcome"], "booked for Monday");
    assert_eq!(cleared["referralState"], "Seen");
    assert_eq!(cleared["referralOutcome"], "");

    let entries = audit_entries(&mut client, "Referral", referral_id).await;
    assert_eq!(changed_fields(&entries[0]), ["ReferralOutcome"]);
    assert_eq!(changed_fields(&entries[1]), ["ReferralOutcome"]);
    assert_eq!(changed_fields(&entries[2]), ["ReferralState"]);
}

#[tokio::test]
async fn outcome_update_fails_for_an_unknown_referral_or_without_an_outcome() {
    let mut client = Client::admin().await;

    let unknown = client.patch("/referrals/99999999/outcome", json!({ "referralOutcome": "x" })).await.expect(StatusCode::NOT_FOUND);
    let missing = client.patch("/referrals/1/outcome", json!({})).await.expect(StatusCode::BAD_REQUEST);

    assert_eq!(unknown.detail(), "Referral was not found.");
    assert_eq!(missing.errors(), [("referralOutcome".to_owned(), "'referral Outcome' must not be empty.".to_owned())]);

    let invalid = client.patch("/referrals/0/outcome", json!({ "referralOutcome": "y".repeat(2001) })).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(
        invalid.errors(),
        [
            ("id".to_owned(), "'id' must be greater than '0'.".to_owned()),
            (
                "referralOutcome".to_owned(),
                "The length of 'referral Outcome' must be 2000 characters or fewer. You entered 2001 characters.".to_owned()
            ),
        ]
    );
}

#[tokio::test]
async fn unknown_referral_state_is_rejected() {
    let mut client = Client::admin().await;
    let suffix = suffix();
    let referral_id = referral_for(&mut client, &format!("Bad state {suffix}"), &format!("state {suffix}")).await;
    let path = format!("/referrals/{referral_id}/state");

    let response = client.patch(&path, json!({ "referralState": "NotAState" })).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(response.errors()[0].0, "referralState");

    let absent = client.patch(&path, json!({ "referralOutcome": "x" })).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(absent.errors(), [("referralState".to_owned(), "'referral State' must not be empty.".to_owned())]);
    client.patch("/referrals/99999999/state", json!({ "referralState": "Seen" })).await.expect(StatusCode::NOT_FOUND);
    assert_eq!(client.get(&format!("/referrals/{referral_id}")).await.ok().json()["referralState"], "New");
}

#[tokio::test]
async fn state_update_without_an_outcome_keeps_the_recorded_one() {
    let mut client = Client::admin().await;
    let suffix = suffix();
    let referral_id = referral_for(&mut client, &format!("Outcome {suffix}"), &format!("outgoing {suffix}")).await;
    let path = format!("/referrals/{referral_id}/state");

    client.patch(&path, json!({ "referralState": "Seen", "referralOutcome": "booked" })).await.expect(StatusCode::OK);
    let outgoing = client.patch(&path, json!({ "referralState": "Outgoing" })).await.expect(StatusCode::OK).json();

    assert_eq!(outgoing["referralState"], "Outgoing");
    assert_eq!(outgoing["referralOutcome"], "booked");
}

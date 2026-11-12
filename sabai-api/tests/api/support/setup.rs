//! Shortcuts for the records most tests start from.

use serde_json::{Value, json};

use super::{Client, suffix};

/// A village code that is unique to the calling test, e.g. `V1A2B3C4D`.
pub fn village_code() -> String {
    format!("V{}", suffix().to_uppercase())
}

/// Creates a village and returns it.
pub async fn create_village(client: &mut Client) -> Value {
    let code = village_code();
    create_village_named(client, &code, &format!("Village {code}")).await
}

pub async fn create_village_named(client: &mut Client, code: &str, name: &str) -> Value {
    client
        .post("/village-codes", json!({ "code": code, "name": name, "colorHex": "#336699", "isVisible": true }))
        .await
        .ok()
        .json()
}

pub fn id(value: &Value) -> i64 {
    value["id"].as_i64().unwrap_or_else(|| panic!("no id in {value}"))
}

/// Registers a patient in `village_id`; the response includes `id` and `visitId`.
pub async fn register_patient(client: &mut Client, village_id: i64) -> Value {
    register_patient_named(client, village_id, &format!("Patient {}", suffix())).await
}

pub async fn register_patient_named(client: &mut Client, village_id: i64, name: &str) -> Value {
    client
        .post(
            "/patients",
            json!({
                "name": name,
                "gender": "female",
                "dateOfBirth": "1991-03-04T00:00:00+00:00",
                "drugAllergy": "None",
                "hasPoorCard": false,
                "hasBS2Card": false,
                "hasSabaiCard": false,
                "villageCodeId": village_id,
            }),
        )
        .await
        .ok()
        .json()
}

/// A new village with one registered patient: `(patient id, visit id)`.
pub async fn patient_with_visit(client: &mut Client) -> (i64, i64) {
    let village = create_village(client).await;
    let patient = register_patient(client, id(&village)).await;
    (id(&patient), patient["visitId"].as_i64().unwrap())
}

/// Records a consult with one respiratory diagnosis on the visit.
pub async fn create_consult(client: &mut Client, visit_id: i64) -> Value {
    client
        .post(
            "/consults",
            json!({
                "visitId": visit_id,
                "consultation": "exam",
                "diagnoses": [{ "details": "cough", "category": "Respiratory" }],
            }),
        )
        .await
        .ok()
        .json()
}

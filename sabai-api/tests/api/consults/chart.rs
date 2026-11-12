use reqwest::StatusCode;
use serde_json::{Value, json};

use super::*;

#[tokio::test]
async fn patient_records_contain_the_patient_and_the_consult() {
    let mut client = Client::admin().await;
    let patient_name = format!("Records {}", suffix());
    let (_, visit_id) = register(&mut client, &patient_name).await;
    let consult_id = consult_with(&mut client, visit_id, &format!("chart wheeze {}", suffix())).await;

    let body = client.get(&format!("/patient-records?visitId={visit_id}")).await.expect(StatusCode::OK).json();

    assert_eq!(body["patient"]["name"], patient_name.as_str());
    assert!(body["consults"].as_array().unwrap().iter().any(|consult| consult["id"] == consult_id));
}

#[tokio::test]
async fn patient_consult_contains_the_doctor_nickname() {
    let mut client = Client::admin().await;
    let (_, visit_id) = register(&mut client, &format!("Workspace {}", suffix())).await;
    consult_with(&mut client, visit_id, &format!("workspace {}", suffix())).await;

    let body = client.get(&format!("/patient-consult?visitId={visit_id}")).await.expect(StatusCode::OK).json();

    assert!(body["consults"].as_array().unwrap().iter().any(|consult| consult["doctorNickname"] == "Admin"));
}

#[tokio::test]
async fn patient_records_reject_visit_id_zero() {
    let mut client = Client::admin().await;

    let response = client.get("/patient-records?visitId=0").await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(
        response.json(),
        json!({ "title": "Bad Request", "status": 400, "detail": "visitId must be a positive integer.", "instance": "/patient-records" })
    );
}

#[tokio::test]
async fn patient_consult_for_a_missing_visit_is_not_found() {
    let mut client = Client::admin().await;

    let response = client.get("/patient-consult?visitId=99999999").await.expect(StatusCode::NOT_FOUND);
    assert_eq!(response.detail(), "Visit was not found.");
}

#[tokio::test]
async fn patient_records_need_a_session() {
    let mut client = Client::anonymous();

    client.get("/patient-records").await.expect(StatusCode::UNAUTHORIZED);
    client.get("/patient-consult?visitId=1").await.expect(StatusCode::UNAUTHORIZED);
}

/// A patient with vitals, an earlier and a later visit, and two consults with
/// prescriptions on the registration visit.
struct Chart {
    village: Value,
    patient: Value,
    visit_id: i64,
    earlier_visit: i64,
    later_visit: i64,
    first_consult: Value,
    second_consult: Value,
    brand_a: String,
    brand_b: String,
}

async fn chart_fixture(admin: &mut Client, doctor: &mut Client) -> Chart {
    let village = create_village(admin).await;
    let patient = admin
        .post(
            "/patients",
            json!({
                "name": format!("សុខ តារា {}", suffix()),
                "gender": "male",
                "dateOfBirth": "2015-06-07T00:00:00+00:00",
                "drugAllergy": "Penicillin",
                "identificationNumber": "ID-7",
                "contactNo": "012 345",
                "hasPoorCard": true,
                "hasBS2Card": true,
                "toGetReport": true,
                "villageCodeId": id(&village),
                "vitals": {
                    "temperature": 36.6, "height": 150, "weight": 51.25, "systolic": 120, "diastolic": 80,
                    "heartRate": 72, "hemocueCount": 12.5, "diabetesMellitus": false, "urineTest": "clear",
                    "bloodGlucoseNonFasting": 6.1, "bloodGlucoseFasting": 5.2, "hba1c": 5.5, "others": "none",
                },
            }),
        )
        .await
        .ok()
        .json();
    let visit_id = patient["visitId"].as_i64().unwrap();
    // Visits, medication brands and order approval have no endpoint in this area: these rows are inserted directly.
    let earlier_visit = visit_on(id(&patient), id(&village), "2020-01-02T03:04:05Z").await;
    let later_visit = visit_on(id(&patient), id(&village), "2031-01-02T03:04:05Z").await;
    let brand_a = format!("Amoxil {}", suffix());
    let brand_b = format!("Brufen {}", suffix());
    let brand_a_id = medication_brand(&brand_a).await;
    let brand_b_id = medication_brand(&brand_b).await;

    let first_consult = doctor
        .post(
            "/consults",
            json!({
                "visitId": visit_id,
                "referredFor": "ENT",
                "diagnoses": [{ "details": "otitis", "category": "Ear Nose Throat" }],
                "orders": [
                    { "medicationBrandId": brand_b_id, "quantity": 3, "dosageInstructions": "bd", "remarks": "after food" },
                    { "medicationBrandId": brand_a_id, "quantity": 1 },
                ],
            }),
        )
        .await
        .ok()
        .json();
    let second_consult = admin
        .post(
            "/consults",
            json!({
                "visitId": visit_id,
                "diagnoses": [{ "details": "review" }],
                "orders": [{ "medicationBrandId": brand_a_id, "quantity": 2, "dosageInstructions": "od", "remarks": "" }],
            }),
        )
        .await
        .ok()
        .json();
    // A consult of another visit must not appear on this visit's chart.
    doctor
        .post(
            "/consults",
            json!({
                "visitId": earlier_visit,
                "diagnoses": [{ "details": "old" }],
                "orders": [{ "medicationBrandId": brand_b_id, "quantity": 9 }],
            }),
        )
        .await
        .ok();
    sqlx::query(r#"UPDATE "Orders" SET "Status" = 'Approved' WHERE "ConsultId" = $1"#)
        .bind(id(&second_consult) as i32)
        .execute(&db().await)
        .await
        .unwrap();

    Chart { village, patient, visit_id, earlier_visit, later_visit, first_consult, second_consult, brand_a, brand_b }
}

impl Chart {
    fn patient_item(&self) -> Value {
        let code = self.village["code"].as_str().unwrap();
        let patient_id = id(&self.patient);
        let name = self.patient["name"].as_str().unwrap();
        json!({
            "id": patient_id,
            "name": name,
            "identificationNumber": "ID-7",
            "contactNo": "012 345",
            "gender": "male",
            "dateOfBirth": "2015-06-07T00:00:00+00:00",
            "drugAllergy": "Penicillin",
            "hasPoorCard": true,
            "hasBS2Card": true,
            "hasSabaiCard": false,
            "toGetReport": true,
            "villageCodeId": id(&self.village),
            "villageCode": code,
            "displayId": format!("{code}{patient_id:04}"),
            "filterString": format!("{code}{patient_id:04} {code}{patient_id} 012 345 {name}"),
            "lastVisitDate": "2031-01-02T03:04:05+00:00",
            "lastVisitId": self.later_visit,
        })
    }

    fn consult_item(consult: &Value, nickname: &str) -> Value {
        json!({ "id": consult["id"], "date": consult["date"], "doctorNickname": nickname, "referredFor": consult["referredFor"] })
    }

    fn prescription(&self, consult: &Value, medication: &str, quantity: i32, notes: Value, status: &str) -> Value {
        json!({
            "consultId": consult["id"],
            "visitDate": self.patient["lastVisitDate"],
            "medicationName": medication,
            "quantity": quantity,
            "notes": notes,
            "status": status,
        })
    }
}

#[tokio::test]
async fn chart_shows_identity_vitals_consults_and_prescriptions_of_the_visit() {
    let mut admin = Client::admin().await;
    let mut doctor = Client::signed_in(DOCTOR).await;
    let chart = chart_fixture(&mut admin, &mut doctor).await;

    let records = admin.get(&format!("/patient-records?visitId={}", chart.visit_id)).await.expect(StatusCode::OK).json();
    let workspace = doctor.get(&format!("/patient-consult?visitId={}", chart.visit_id)).await.expect(StatusCode::OK).json();

    let first = Chart::consult_item(&chart.first_consult, "Doctor");
    let second = Chart::consult_item(&chart.second_consult, "Admin");
    assert_eq!(first["referredFor"], "ENT");
    assert_eq!(second["referredFor"], Value::Null);
    let expected = json!({
        "patient": chart.patient_item(),
        "vitals": {
            "id": chart.patient["vitalsId"],
            "visitId": chart.visit_id,
            "height": 150.0,
            "weight": 51.25,
            "temperature": 36.6,
            "systolic": 120,
            "diastolic": 80,
            "heartRate": 72,
            "hemocueCount": 12.5,
            "diabetesMellitus": false,
            "urineTest": "clear",
            "bloodGlucoseNonFasting": 6.1,
            "bloodGlucoseFasting": 5.2,
            "hba1c": 5.5,
            "others": "none",
        },
        "visitDate": chart.patient["lastVisitDate"],
        // The records view lists consults newest first.
        "consults": [second, first],
        // Prescriptions follow their consult's date, then the order they were written in.
        "prescriptions": [
            chart.prescription(&chart.first_consult, &chart.brand_b, 3, json!("after food"), "PENDING"),
            chart.prescription(&chart.first_consult, &chart.brand_a, 1, Value::Null, "PENDING"),
            chart.prescription(&chart.second_consult, &chart.brand_a, 2, json!(""), "APPROVED"),
        ],
    });
    assert_eq!(records, expected);

    // The consult workspace is the same chart with consults oldest first.
    let mut expected_workspace = expected.clone();
    expected_workspace["consults"] = json!([first, second]);
    assert_eq!(workspace, expected_workspace);
}

#[tokio::test]
async fn chart_of_a_visit_without_records_has_no_vitals_and_empty_lists() {
    let mut admin = Client::admin().await;
    let mut doctor = Client::signed_in(DOCTOR).await;
    let chart = chart_fixture(&mut admin, &mut doctor).await;

    for path in ["/patient-records", "/patient-consult"] {
        let later = admin.get(&format!("{path}?visitId={}", chart.later_visit)).await.expect(StatusCode::OK).json();
        assert_eq!(
            later,
            json!({
                "patient": chart.patient_item(),
                "vitals": null,
                "visitDate": "2031-01-02T03:04:05+00:00",
                "consults": [],
                "prescriptions": [],
            })
        );

        let earlier = admin.get(&format!("{path}?visitId={}", chart.earlier_visit)).await.expect(StatusCode::OK).json();
        assert_eq!(earlier["visitDate"], "2020-01-02T03:04:05+00:00");
        assert_eq!(earlier["patient"]["lastVisitId"], chart.later_visit, "the last visit is the patient's latest by date");
        assert_eq!(earlier["consults"].as_array().unwrap().len(), 1);
        assert_eq!(earlier["prescriptions"][0]["quantity"], 9);
        assert_eq!(earlier["prescriptions"][0]["visitDate"], "2020-01-02T03:04:05+00:00");
    }
}

#[tokio::test]
async fn chart_consults_made_at_the_same_moment_are_ordered_by_id() {
    let mut client = Client::admin().await;
    let (_, visit_id) = register(&mut client, &format!("Ties {}", suffix())).await;
    let first = consult_with(&mut client, visit_id, "a").await;
    let second = consult_with(&mut client, visit_id, "b").await;
    let third = consult_with(&mut client, visit_id, "c").await;
    sqlx::query(r#"UPDATE "Consults" SET "Date" = '2026-01-01T00:00:00Z' WHERE "VisitId" = $1"#)
        .bind(visit_id as i32)
        .execute(&db().await)
        .await
        .unwrap();

    let ids = |body: Value| -> Vec<i64> { body["consults"].as_array().unwrap().iter().map(id).collect() };
    let records = client.get(&format!("/patient-records?visitId={visit_id}")).await.ok().json();
    let workspace = client.get(&format!("/patient-consult?visitId={visit_id}")).await.ok().json();

    assert_eq!(ids(records), [third, second, first]);
    assert_eq!(ids(workspace), [first, second, third]);
}

#[tokio::test]
async fn chart_rejects_visit_ids_that_are_not_positive_integers() {
    let mut client = Client::admin().await;

    for path in ["/patient-records", "/patient-consult"] {
        for query in ["", "?visitId=0", "?visitId=-3"] {
            let response = client.get(&format!("{path}{query}")).await.expect(StatusCode::BAD_REQUEST);
            assert_eq!(response.detail(), "visitId must be a positive integer.");
        }
        let not_a_number = client.get(&format!("{path}?visitId=abc")).await.expect(StatusCode::BAD_REQUEST);
        assert_eq!(
            not_a_number.errors(),
            [("visitId".to_owned(), "Value [abc] is not valid for a [Int32] property!".to_owned())]
        );
    }
}

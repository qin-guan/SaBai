use reqwest::StatusCode;
use serde_json::{Value, json};

use crate::patients::{code, register, village};
use crate::support::*;

fn visits(listed: &Value) -> &Vec<Value> {
    listed["visits"].as_array().expect("a visits list")
}

fn visit<'a>(listed: &'a Value, visit_id: &Value) -> &'a Value {
    let mut matches = visits(listed).iter().filter(|x| &x["id"] == visit_id);
    let found = matches.next().unwrap_or_else(|| panic!("visit {visit_id} is not in {listed}"));
    assert!(matches.next().is_none(), "visit {visit_id} is listed twice");
    found
}

#[tokio::test]
async fn visit_lists_include_the_village() {
    let mut client = Client::admin().await;
    let code = code();
    let village = village(&mut client, &code, "#778899", &format!("Seen {code}")).await;
    let patient = register(&mut client, id(&village), &format!("Visited {code}"), None, false).await;

    let listed = client.get(&format!("/visits?patientId={}", id(&patient))).await.ok().json();
    let by_patient = client.get(&format!("/patients/{}/visits", id(&patient))).await.ok().json();
    let visit_row = visit(&listed, &patient["visitId"]);
    let nested = visit(&by_patient, &patient["visitId"]);

    assert_eq!(visit_row["patientName"], patient["name"]);
    assert_eq!(visit_row["villageCode"], code);
    assert_eq!(visit_row["villageName"], village["name"]);
    assert_eq!(visit_row["villageColorHex"], "#778899");
    assert_eq!(visit_row["status"], "started");
    assert_eq!(nested["villageCode"], code);
    assert_eq!(nested["villageName"], village["name"]);
    assert_eq!(nested["villageColorHex"], "#778899");
}

#[tokio::test]
async fn patient_visits_include_the_journey() {
    let mut client = Client::admin().await;
    let code = code();
    let village = create_village(&mut client).await;
    let patient = register(&mut client, id(&village), &format!("Journey {code}"), None, true).await;

    let by_patient = client.get(&format!("/patients/{}/visits", id(&patient))).await.ok().json();
    let latest = client.get("/patients/with-latest-visit").await.ok().json();
    let journey = &visit(&by_patient, &patient["visitId"])["journey"];
    let row = latest["patients"].as_array().unwrap().iter().find(|x| x["id"] == patient["id"]).unwrap();

    assert_eq!(journey["vitals"], true);
    assert!(journey["vitalsAt"].is_string());
    assert_eq!(journey["vision"], false);
    assert_eq!(journey["consult"], false);
    assert_eq!(journey["dental"], false);
    assert_eq!(journey["pharmacyOrders"], 0);
    assert_eq!(&row["lastVisitJourney"], journey);
    assert_eq!(
        journey,
        &json!({
            "vitals": true, "vision": false, "consult": false, "dental": false, "dentalReferred": false,
            "pharmacyOrders": 0, "pharmacyPending": 0, "vitalsAt": journey["vitalsAt"], "visionAt": null,
            "consultAt": null, "dentalAt": null, "pharmacyAt": null,
        })
    );
}

#[tokio::test]
async fn journey_follows_consults_referrals_and_orders() {
    let mut client = Client::signed_in(DOCTOR).await;
    let (patient_id, visit_id) = patient_with_visit(&mut client).await;
    let consult = client
        .post(
            "/consults",
            json!({
                "visitId": visit_id,
                "consultation": "exam",
                "referredFor": "Dental clinic",
                "diagnoses": [{ "details": "toothache", "category": "Oral Health" }],
            }),
        )
        .await
        .ok()
        .json();
    // Orders have no endpoint in this slice's worktree; the rows are inserted directly.
    let db = db().await;
    let brand: i32 = sqlx::query_scalar(
        r#"WITH ingredient AS (
               INSERT INTO "MedicationActiveIngredients" ("Name", "UnitOfMeasurement", "FallBelow")
               VALUES ($1, 'tablet', 0) RETURNING "Id")
           INSERT INTO "MedicationBrands" ("Name", "ActiveIngredientId")
           SELECT $1, "Id" FROM ingredient RETURNING "Id""#,
    )
    .bind(format!("Journey {}", suffix()))
    .fetch_one(&db)
    .await
    .unwrap();
    for (status, approved_at) in [("Pending", None), ("Approved", Some("2026-01-02T03:04:05Z")), ("Cancelled", None)] {
        sqlx::query(
            r#"INSERT INTO "Orders" ("ConsultId", "MedicationBrandId", "DosageInstructions", "Status", "Quantity", "ApprovedAt", "CreatedAt")
               VALUES ($1, $2, 'once daily', $3, 1, $4::timestamptz, now())"#,
        )
        .bind(consult["id"].as_i64().unwrap() as i32)
        .bind(brand)
        .bind(status)
        .bind(approved_at)
        .execute(&db)
        .await
        .unwrap();
    }

    let by_patient = client.get(&format!("/patients/{patient_id}/visits")).await.ok().json();
    let journey = &visits(&by_patient)[0]["journey"];

    assert_eq!(journey["vitals"], false);
    assert_eq!(journey["vitalsAt"], Value::Null);
    assert_eq!(journey["consult"], true);
    assert_eq!(journey["consultAt"], consult["date"]);
    assert_eq!(journey["dentalReferred"], true);
    assert_eq!(journey["dental"], false);
    assert_eq!(journey["pharmacyOrders"], 2);
    assert_eq!(journey["pharmacyPending"], 1);
    assert_eq!(journey["pharmacyAt"], "2026-01-02T03:04:05+00:00");
}

#[tokio::test]
async fn visit_without_a_consult_can_be_deleted() {
    let mut client = Client::admin().await;
    let village = create_village(&mut client).await;
    let patient = register(&mut client, id(&village), &format!("Closable {}", code()), None, true).await;
    let path = format!("/visits/{}", patient["visitId"]);

    client.delete(&path).await.expect(StatusCode::NO_CONTENT);
    let missing = client.get(&path).await.expect(StatusCode::NOT_FOUND);

    assert_eq!(missing.detail(), "Visit was not found.");
}

#[tokio::test]
async fn recent_visit_is_reported_right_after_registration() {
    let mut client = Client::admin().await;
    let village = create_village(&mut client).await;
    let patient = register(&mut client, id(&village), &format!("Repeat {}", code()), None, false).await;

    let recent = client.get(&format!("/visits/recent?patientId={}", id(&patient))).await.ok().json();

    assert_eq!(recent["hasRecentVisit"], true);
    assert_eq!(recent["existingVisitDate"], patient["lastVisitDate"]);
}

#[tokio::test]
async fn no_recent_visit_without_a_visit() {
    let mut client = Client::admin().await;
    let village = create_village(&mut client).await;
    let patient = register(&mut client, id(&village), &format!("Fresh {}", code()), None, false).await;
    client.delete(&format!("/visits/{}", patient["visitId"])).await.ok();

    let recent = client.get(&format!("/visits/recent?patientId={}", id(&patient))).await.ok().json();

    assert_eq!(recent, json!({ "hasRecentVisit": false, "existingVisitDate": null }));
}

#[tokio::test]
async fn visits_older_than_a_minute_are_not_recent() {
    let mut client = Client::admin().await;
    let (patient_id, visit_id) = patient_with_visit(&mut client).await;
    let two_minutes_ago = (chrono::Utc::now() - chrono::Duration::minutes(2)).to_rfc3339();
    client.patch(&format!("/visits/{visit_id}"), json!({ "date": two_minutes_ago })).await.ok();

    let recent = client.get(&format!("/visits/recent?patientId={patient_id}")).await.ok().json();
    assert_eq!(recent["hasRecentVisit"], false);

    let invalid = client.get("/visits/recent").await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(invalid.errors(), [("patientId".to_owned(), "'patient Id' must be greater than '0'.".to_owned())]);
}

#[tokio::test]
async fn patched_status_is_returned_on_get() {
    let mut client = Client::admin().await;
    let (_, visit_id) = patient_with_visit(&mut client).await;
    let path = format!("/visits/{visit_id}");

    let patched = client.patch(&path, json!({ "status": "completed" })).await.expect(StatusCode::OK).json();
    let fetched = client.get(&path).await.expect(StatusCode::OK).json();

    assert_eq!(fetched["status"], "completed");
    assert_eq!(fetched, patched);
}

#[tokio::test]
async fn patient_visits_contain_the_registration_visit() {
    let mut client = Client::admin().await;
    let (patient_id, visit_id) = patient_with_visit(&mut client).await;

    let body = client.get(&format!("/patients/{patient_id}/visits")).await.expect(StatusCode::OK).json();

    assert!(visits(&body).iter().any(|x| id(x) == visit_id));
}

#[tokio::test]
async fn visit_for_a_missing_patient_is_not_found() {
    let mut client = Client::admin().await;
    let village = create_village(&mut client).await;

    let response = client
        .post("/visits", json!({ "patientId": 99999999, "villageCodeId": id(&village) }))
        .await
        .expect(StatusCode::NOT_FOUND);

    assert_eq!(response.detail(), "Patient was not found.");
}

#[tokio::test]
async fn missing_visit_is_not_found() {
    let mut client = Client::admin().await;

    let response = client.get("/visits/99999999").await.expect(StatusCode::NOT_FOUND);

    assert_eq!(response.detail(), "Visit was not found.");
}

#[tokio::test]
async fn visit_with_a_consult_cannot_be_deleted() {
    let mut client = Client::signed_in(DOCTOR).await;
    let (_, visit_id) = patient_with_visit(&mut client).await;
    create_consult(&mut client, visit_id).await;

    let response = client.delete(&format!("/visits/{visit_id}")).await.expect(StatusCode::CONFLICT);

    assert_eq!(response.detail(), "A consult still references this visit.");
}

#[tokio::test]
async fn visits_require_a_session() {
    let mut client = Client::anonymous();

    let response = client.post("/visits", json!({ "patientId": 1, "villageCodeId": 1 })).await;

    assert_eq!(response.status, StatusCode::UNAUTHORIZED);
}

#[tokio::test]
async fn visit_can_be_opened_moved_and_listed_newest_first() {
    let mut client = Client::admin().await;
    let code = code();
    let village = village(&mut client, &code, "#010203", &format!("Village {code}")).await;
    let other_village = create_village(&mut client).await;
    let patient = register(&mut client, id(&village), &format!("Returning {code}"), None, false).await;
    let other_patient = register_patient(&mut client, id(&other_village)).await;

    let created = client
        .post(
            "/visits",
            json!({ "patientId": id(&patient), "villageCodeId": id(&village), "date": "2020-02-03T04:05:06.1234567Z", "status": "  waiting " }),
        )
        .await
        .expect(StatusCode::CREATED);
    let earlier = created.json();
    assert_eq!(created.header("location"), Some(format!("/visits/{}", id(&earlier)).as_str()));
    assert_eq!(
        earlier,
        json!({
            "id": earlier["id"], "date": "2020-02-03T04:05:06.123456+00:00", "patientId": patient["id"],
            "patientName": patient["name"], "villageCodeId": village["id"], "villageCode": code,
            "villageName": village["name"], "villageColorHex": "#010203", "status": "waiting",
        })
    );
    let defaulted = client
        .post("/visits", json!({ "patientId": id(&patient), "villageCodeId": id(&village) }))
        .await
        .expect(StatusCode::CREATED)
        .json();
    assert_eq!(defaulted["status"], "started");

    let listed = client.get(&format!("/visits?patientId={}", id(&patient))).await.ok().json();
    let ids: Vec<&Value> = visits(&listed).iter().map(|x| &x["id"]).collect();
    assert_eq!(ids, [&defaulted["id"], &patient["visitId"], &earlier["id"]]);
    let everything = client.get("/visits").await.ok().json();
    assert!(visits(&everything).iter().any(|x| x["id"] == other_patient["visitId"]));

    let moved = client
        .patch(
            &format!("/visits/{}", id(&earlier)),
            json!({ "patientId": id(&other_patient), "villageCodeId": id(&other_village), "date": "2021-01-01T00:00:00Z", "status": " done " }),
        )
        .await
        .expect(StatusCode::OK)
        .json();
    assert_eq!(moved["patientName"], other_patient["name"]);
    assert_eq!(moved["villageCode"], other_village["code"]);
    assert_eq!(moved["date"], "2021-01-01T00:00:00+00:00");
    assert_eq!(moved["status"], "done");

    let invalid = client.post("/visits", json!({ "status": "" })).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(
        invalid.errors(),
        [
            ("patientId".to_owned(), "'patient Id' must be greater than '0'.".to_owned()),
            ("villageCodeId".to_owned(), "'village Code Id' must be greater than '0'.".to_owned()),
            ("status".to_owned(), "'status' must not be empty.".to_owned()),
        ]
    );
    let unknown_village = client
        .patch(&format!("/visits/{}", id(&earlier)), json!({ "villageCodeId": 99999999 }))
        .await
        .expect(StatusCode::NOT_FOUND);
    assert_eq!(unknown_village.detail(), "Village code was not found.");
}

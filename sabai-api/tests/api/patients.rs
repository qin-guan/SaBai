use reqwest::StatusCode;
use serde_json::{Value, json};

use crate::support::*;

/// Registers a patient the way the clinic's registration desk does; the response
/// includes `id` and `visitId`.
pub async fn register(
    client: &mut Client,
    village_id: i64,
    name: &str,
    identification_number: Option<&str>,
    with_vitals: bool,
) -> Value {
    let vitals = with_vitals.then(|| {
        json!({
            "temperature": 36.6, "height": 160, "weight": 55, "systolic": 120, "diastolic": 80, "heartRate": 72,
            "hemocueCount": 12.5, "diabetesMellitus": false, "urineTest": "negative",
            "bloodGlucoseNonFasting": 5.5, "bloodGlucoseFasting": 4.8, "hba1c": 5.4, "others": "none",
        })
    });
    client
        .post(
            "/patients",
            json!({
                "name": name,
                "identificationNumber": identification_number,
                "contactNo": "012345678",
                "gender": "female",
                "dateOfBirth": "1990-05-01T00:00:00+00:00",
                "drugAllergy": "None",
                "hasPoorCard": false,
                "hasBS2Card": true,
                "hasSabaiCard": false,
                "toGetReport": true,
                "villageCodeId": village_id,
                "vitals": vitals,
            }),
        )
        .await
        .ok()
        .json()
}

/// A short code that is unique to the calling test, like `a1b2c3`.
pub fn code() -> String {
    suffix()[..6].to_owned()
}

pub async fn village(client: &mut Client, code: &str, color_hex: &str, name: &str) -> Value {
    client
        .post("/village-codes", json!({ "code": code, "name": name, "colorHex": color_hex, "isVisible": true }))
        .await
        .ok()
        .json()
}

fn patients(listed: &Value) -> &Vec<Value> {
    listed["patients"].as_array().expect("a patients list")
}

/// A small PNG, for photo uploads.
pub fn png() -> Vec<u8> {
    let image = image::RgbImage::from_pixel(6, 4, image::Rgb([200, 30, 30]));
    let mut bytes = std::io::Cursor::new(Vec::new());
    image.write_to(&mut bytes, image::ImageFormat::Png).unwrap();
    bytes.into_inner()
}

async fn put_photo(client: &mut Client, patient_id: i64, part: reqwest::multipart::Part) -> Response {
    let form = reqwest::multipart::Form::new().part("image", part);
    let request = client.request(reqwest::Method::PUT, &format!("/patients/{patient_id}/photo")).multipart(form);
    client.send(request).await
}

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

#[tokio::test]
async fn registration_persists_the_patient_the_visit_and_the_vitals() {
    let mut client = Client::admin().await;
    let code = code();
    let village = village(&mut client, &code, "#102030", &format!("Reg {code}")).await;

    let created = client
        .post(
            "/patients",
            json!({
                "name": "Siti Aminah",
                "identificationNumber": format!("NID-{code}"),
                "contactNo": "011223344",
                "gender": "female",
                "dateOfBirth": "1990-05-01T00:00:00+00:00",
                "drugAllergy": "None",
                "hasPoorCard": true,
                "hasBS2Card": false,
                "hasSabaiCard": true,
                "toGetReport": true,
                "villageCodeId": id(&village),
                "vitals": { "temperature": 36.6, "height": 160, "weight": 55, "systolic": 120, "diastolic": 80, "heartRate": 72 },
            }),
        )
        .await
        .expect(StatusCode::CREATED);
    let body = created.json();
    let number = id(&body);
    let visit = client.get(&format!("/visits/{}", body["visitId"])).await.ok().json();
    let patient = client.get(&format!("/patients/{number}")).await.ok().json();

    assert_eq!(created.header("location"), Some(format!("/patients/{number}").as_str()));
    assert_eq!(body["displayId"], format!("{code}{number:04}"));
    assert!(body["filterString"].as_str().unwrap().contains("Siti Aminah"));
    assert_eq!(body["villageCode"], code);
    assert_eq!(body["gender"], "female");
    assert_eq!(body["drugAllergy"], "None");
    assert!(body["visitId"].as_i64().unwrap() > 0);
    assert!(body["vitalsId"].is_i64());
    assert_eq!(body["lastVisitId"], body["visitId"]);
    assert_eq!(visit["status"], "started");
    assert_eq!(visit["patientName"], "Siti Aminah");
    assert_eq!(visit["villageCode"], code);
    assert_eq!(visit["villageColorHex"], "#102030");
    assert_eq!(patient["displayId"], body["displayId"]);
    assert_eq!(patient["hasPoorCard"], true);
    assert_eq!(patient["hasSabaiCard"], true);
    assert_eq!(patient["lastVisitId"], body["visitId"]);
    assert_eq!(patient["lastVisitDate"], body["lastVisitDate"]);
}

#[tokio::test]
async fn list_filters_by_name_and_village_code_ignoring_case() {
    let mut client = Client::admin().await;
    let code = code().to_uppercase();
    let village = village(&mut client, &code, "#336699", &format!("Village {code}")).await;
    let name = format!("Filter {code}");
    let patient = register(&mut client, id(&village), &name, None, false).await;
    let number = id(&patient);

    let matched = client
        .get(&format!("/patients?name={}&villageCode={}", name.to_lowercase().replace(' ', "%20"), code.to_lowercase()))
        .await
        .ok()
        .json();
    let missed = client.get(&format!("/patients?name=zz{}", suffix())).await.ok().json();

    assert!(
        patients(&matched).iter().any(|x| id(x) == number && x["displayId"] == format!("{code}{number:04}")),
        "{matched}"
    );
    assert!(!patients(&missed).iter().any(|x| id(x) == number));
}

#[tokio::test]
async fn list_matches_the_name_literally_and_orders_by_id() {
    let mut client = Client::admin().await;
    let code = code();
    let village = village(&mut client, &code, "#336699", &format!("Village {code}")).await;
    let other_village = create_village(&mut client).await;
    let first = register(&mut client, id(&village), &format!("100%_{code} A"), None, false).await;
    let second = register(&mut client, id(&village), &format!("100%_{code} B"), None, false).await;
    let elsewhere = register(&mut client, id(&other_village), &format!("100%_{code} C"), None, false).await;

    let by_name = client.get(&format!("/patients?name=%20100%25_{code}%20")).await.ok().json();
    let ids: Vec<i64> = patients(&by_name).iter().map(id).collect();
    assert_eq!(ids, [id(&first), id(&second), id(&elsewhere)]);

    // `%` and `_` are ordinary characters, not wildcards.
    let wildcard = client.get(&format!("/patients?name=100_{code}")).await.ok().json();
    assert!(patients(&wildcard).is_empty());
    let wildcard = client.get(&format!("/patients?name=1%25{code}")).await.ok().json();
    assert!(patients(&wildcard).is_empty());

    let in_village = client.get(&format!("/patients?villageCode=%20{}%20", code.to_uppercase())).await.ok().json();
    let ids: Vec<i64> = patients(&in_village).iter().map(id).collect();
    assert_eq!(ids, [id(&first), id(&second)]);
    let row = &patients(&in_village)[0];
    assert_eq!(row["filterString"], format!("{code}{0:04} {code}{0} 012345678 100%_{code} A", id(&first)));
    assert_eq!(row["lastVisitId"], first["visitId"]);
    assert_eq!(row["hasBS2Card"], true);

    // Blank filters are no filters.
    let everyone = client.get("/patients?name=%20&villageCode=").await.ok().json();
    assert!(patients(&everyone).iter().any(|x| id(x) == id(&elsewhere)));
}

#[tokio::test]
async fn with_latest_visit_puts_visited_patients_before_visitless_ones() {
    let mut client = Client::admin().await;
    let code = code();
    let village = village(&mut client, &code, "#ABCDEF", &format!("Village {code}")).await;
    let recent_name = format!("Recent {code}");
    let older_name = format!("Older {code}");
    let none_name = format!("None {code}");
    let recent = register(&mut client, id(&village), &recent_name, None, false).await;
    let older = register(&mut client, id(&village), &older_name, None, false).await;
    let none = register(&mut client, id(&village), &none_name, None, false).await;
    let two_days_ago = (chrono::Utc::now() - chrono::Duration::days(2)).to_rfc3339();
    client.patch(&format!("/visits/{}", older["visitId"]), json!({ "date": two_days_ago })).await.expect(StatusCode::OK);
    client.delete(&format!("/visits/{}", none["visitId"])).await.expect(StatusCode::NO_CONTENT);

    let listed = client.get("/patients/with-latest-visit").await.ok().json();
    let names: Vec<&str> = patients(&listed).iter().map(|x| x["name"].as_str().unwrap()).collect();
    let position = |name: &str| names.iter().position(|x| *x == name).unwrap();
    let recent_row = patients(&listed).iter().find(|x| id(x) == id(&recent)).unwrap();
    let none_row = patients(&listed).iter().find(|x| id(x) == id(&none)).unwrap();

    assert!(position(&recent_name) < position(&older_name));
    assert!(position(&older_name) < position(&none_name));
    assert_eq!(recent_row["villageColorHex"], "#ABCDEF");
    assert_eq!(recent_row["lastVisitId"], recent["visitId"]);
    assert_eq!(recent_row["lastVisitJourney"]["vitals"], false);
    assert_eq!(none_row["lastVisitId"], Value::Null);
    assert_eq!(none_row["lastVisitDate"], Value::Null);
    assert_eq!(none_row["lastVisitJourney"], Value::Null);
}

#[tokio::test]
async fn patch_with_another_patients_identification_number_is_a_conflict() {
    let mut client = Client::admin().await;
    let code = code();
    let village = create_village(&mut client).await;
    let identification_number = format!("NID-{}", suffix());
    register(&mut client, id(&village), &format!("First {code}"), Some(&identification_number), false).await;
    let second =
        register(&mut client, id(&village), &format!("Second {code}"), Some(&format!("NID-{}", suffix())), false).await;

    let conflict = client
        .patch(&format!("/patients/{}", id(&second)), json!({ "identificationNumber": format!("  {identification_number}  ") }))
        .await
        .expect(StatusCode::CONFLICT);

    assert_eq!(conflict.detail(), "Another patient already has this identification number.");
}

#[tokio::test]
async fn patch_changes_only_what_is_sent() {
    let mut client = Client::admin().await;
    let code = code();
    let village = village(&mut client, &code, "#336699", &format!("Village {code}")).await;
    let other = create_village(&mut client).await;
    let patient = register(&mut client, id(&village), "Before", Some(&format!("NID-{}", suffix())), false).await;
    let path = format!("/patients/{}", id(&patient));

    let updated = client
        .patch(
            &path,
            json!({
                "name": " After ", "identificationNumber": "  ", "contactNo": " ", "gender": "male",
                "dateOfBirth": "2000-02-02", "drugAllergy": " Penicillin ", "hasPoorCard": true, "hasBS2Card": false,
                "villageCodeId": id(&other),
            }),
        )
        .await
        .expect(StatusCode::OK)
        .json();
    assert_eq!(updated["name"], "After");
    assert_eq!(updated["identificationNumber"], Value::Null);
    assert_eq!(updated["contactNo"], Value::Null);
    assert_eq!(updated["gender"], "male");
    assert_eq!(updated["dateOfBirth"], "2000-02-02T00:00:00+00:00");
    assert_eq!(updated["drugAllergy"], "Penicillin");
    assert_eq!(updated["hasPoorCard"], true);
    assert_eq!(updated["hasBS2Card"], false);
    assert_eq!(updated["toGetReport"], true);
    assert_eq!(updated["villageCode"], other["code"]);
    assert_eq!(updated["lastVisitId"], patient["visitId"]);
    assert_eq!(client.get(&path).await.ok().json(), updated);

    let unchanged = client.patch(&path, json!({ "villageCodeId": null })).await.ok().json();
    assert_eq!(unchanged, updated);

    let invalid = client
        .patch(&path, json!({ "name": "", "drugAllergy": " ", "villageCodeId": 0 }))
        .await
        .expect(StatusCode::BAD_REQUEST);
    assert_eq!(
        invalid.errors(),
        [
            ("name".to_owned(), "'name' must not be empty.".to_owned()),
            ("drugAllergy".to_owned(), "'drug Allergy' must not be empty.".to_owned()),
            ("villageCodeId".to_owned(), "'village Code Id' must be greater than '0'.".to_owned()),
        ]
    );
    let unknown_village = client.patch(&path, json!({ "villageCodeId": 99999999 })).await.expect(StatusCode::NOT_FOUND);
    assert_eq!(unknown_village.detail(), "Village code was not found.");
}

#[tokio::test]
async fn patient_with_a_visit_cannot_be_deleted() {
    let mut client = Client::admin().await;
    let village = create_village(&mut client).await;
    let patient = register(&mut client, id(&village), &format!("Kept {}", code()), None, false).await;

    let deleted = client.delete(&format!("/patients/{}", id(&patient))).await.expect(StatusCode::CONFLICT);

    assert_eq!(deleted.detail(), "Patient has visits and cannot be deleted.");
}

#[tokio::test]
async fn patient_without_visits_is_deleted_with_their_photo() {
    let mut client = Client::admin().await;
    let (patient_id, visit_id) = patient_with_visit(&mut client).await;
    let path = format!("/patients/{patient_id}");
    let part = reqwest::multipart::Part::bytes(png()).file_name("face.png").mime_str("image/png").unwrap();
    put_photo(&mut client, patient_id, part).await.expect(StatusCode::OK);
    client.delete(&format!("/visits/{visit_id}")).await.expect(StatusCode::NO_CONTENT);

    client.delete(&path).await.expect(StatusCode::NO_CONTENT);

    let gone = client.get(&path).await.expect(StatusCode::NOT_FOUND);
    assert_eq!(gone.detail(), "Patient was not found.");
    let again = client.delete(&path).await.expect(StatusCode::NOT_FOUND);
    assert_eq!(again.detail(), "Patient was not found.");
    let log = client.get(&format!("/audit-logs?entityType=Patient&entityId={patient_id}")).await.ok().json();
    assert_eq!(log["entries"][0]["action"], "Deleted");
}

#[tokio::test]
async fn patients_require_a_session() {
    let mut client = Client::anonymous();

    client.get("/patients").await.expect(StatusCode::UNAUTHORIZED);
    client.get("/patients/with-latest-visit").await.expect(StatusCode::UNAUTHORIZED);
    client.get("/patients/1/photo").await.expect(StatusCode::UNAUTHORIZED);
}

#[tokio::test]
async fn get_returns_name_and_display_id() {
    let mut client = Client::admin().await;
    let code = village_code();
    let name = format!("Patient {}", suffix());
    let village = create_village_named(&mut client, &code, &format!("Village {code}")).await;
    let patient = register_patient_named(&mut client, id(&village), &name).await;

    let body = client.get(&format!("/patients/{}", id(&patient))).await.expect(StatusCode::OK).json();

    assert_eq!(body["name"], name);
    assert!(body["displayId"].as_str().unwrap().starts_with(&code));
    assert_eq!(body["hasPhoto"], false);
    assert_eq!(body["hasFace"], false);
}

#[tokio::test]
async fn registration_in_a_missing_village_is_not_found() {
    let mut client = Client::admin().await;

    let response = client
        .post(
            "/patients",
            json!({
                "name": format!("Patient {}", suffix()), "gender": "female", "drugAllergy": "None",
                "villageCodeId": 99999999, "dateOfBirth": "1991-03-04T00:00:00+00:00",
            }),
        )
        .await;

    assert_eq!(response.status, StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn registration_without_gender_is_rejected() {
    let mut client = Client::admin().await;
    let village = create_village(&mut client).await;

    let response = client
        .post(
            "/patients",
            json!({
                "name": format!("Patient {}", suffix()), "drugAllergy": "None",
                "villageCodeId": id(&village), "dateOfBirth": "1991-03-04T00:00:00+00:00",
            }),
        )
        .await;

    assert_eq!(response.status, StatusCode::BAD_REQUEST);
}

#[tokio::test]
async fn missing_patient_is_not_found() {
    let mut client = Client::admin().await;

    let response = client.get("/patients/99999999").await.expect(StatusCode::NOT_FOUND);

    assert_eq!(
        response.json(),
        json!({ "title": "Not Found", "status": 404, "detail": "Patient was not found.", "instance": "/patients/99999999" })
    );
}

#[tokio::test]
async fn patching_a_missing_patient_is_not_found() {
    let mut client = Client::admin().await;

    let response = client.patch("/patients/99999999", json!({ "name": "Missing" })).await;

    assert_eq!(response.status, StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn photo_can_be_set_replaced_downloaded_and_removed() {
    let mut client = Client::admin().await;
    let (patient_id, _) = patient_with_visit(&mut client).await;
    let path = format!("/patients/{patient_id}");
    let photo_path = format!("{path}/photo");

    let none = client.get(&photo_path).await.expect(StatusCode::NOT_FOUND);
    assert_eq!(none.detail(), "Patient has no photo.");

    let part = reqwest::multipart::Part::bytes(png()).file_name("My Photo.PNG").mime_str("image/png").unwrap();
    let set = put_photo(&mut client, patient_id, part).await.expect(StatusCode::OK);
    assert_eq!(set.json(), json!({ "id": patient_id, "hasPhoto": true }));
    assert_eq!(client.get(&path).await.ok().json()["hasPhoto"], true);

    let downloaded = client.get(&photo_path).await.expect(StatusCode::OK);
    assert_eq!(downloaded.bytes, png());
    assert_eq!(downloaded.header("content-type"), Some("image/png"));
    assert_eq!(downloaded.header("content-length"), Some(png().len().to_string().as_str()));
    assert_eq!(
        downloaded.header("content-disposition"),
        Some(format!("attachment; filename=patient-{patient_id}.png; filename*=UTF-8''patient-{patient_id}.png").as_str())
    );

    // No content type and no extension: stored as a JPEG.
    let replaced = put_photo(&mut client, patient_id, reqwest::multipart::Part::bytes(png()).file_name("photo")).await;
    replaced.expect(StatusCode::OK);
    let downloaded = client.get(&photo_path).await.expect(StatusCode::OK);
    assert_eq!(downloaded.header("content-type"), Some("image/jpeg"));
    assert!(downloaded.header("content-disposition").unwrap().contains(&format!("filename=patient-{patient_id}.jpg;")));

    client.delete(&photo_path).await.expect(StatusCode::NO_CONTENT);
    assert_eq!(client.get(&path).await.ok().json()["hasPhoto"], false);
    client.get(&photo_path).await.expect(StatusCode::NOT_FOUND);
    client.delete(&photo_path).await.expect(StatusCode::NO_CONTENT);
}

#[tokio::test]
async fn photo_uploads_are_validated() {
    let mut client = Client::admin().await;
    let (patient_id, _) = patient_with_visit(&mut client).await;
    let image = |reason: &str| vec![("image".to_owned(), reason.to_owned())];

    let not_an_image = reqwest::multipart::Part::bytes(b"clinic note".to_vec()).file_name("a.png").mime_str("image/png").unwrap();
    let rejected = put_photo(&mut client, patient_id, not_an_image).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(rejected.errors(), image("Unsupported or corrupt image."));
    assert_eq!(rejected.detail(), "Unsupported or corrupt image.");

    let empty = reqwest::multipart::Part::bytes(Vec::new()).file_name("a.png").mime_str("image/png").unwrap();
    let rejected = put_photo(&mut client, patient_id, empty).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(rejected.errors(), image("An image file is required."));

    let form = reqwest::multipart::Form::new().part("photo", reqwest::multipart::Part::bytes(png()).file_name("a.png"));
    let request = client.request(reqwest::Method::PUT, &format!("/patients/{patient_id}/photo")).multipart(form);
    let rejected = client.send(request).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(rejected.errors(), image("An image file is required."));

    let too_large = reqwest::multipart::Part::bytes(vec![0; 10 * 1024 * 1024 + 1]).file_name("a.png");
    let rejected = put_photo(&mut client, patient_id, too_large).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(rejected.errors(), image("Image is too large."));

    // Just past the limit, so the whole request is sent before the server refuses it.
    let over_the_body_limit = reqwest::multipart::Part::bytes(vec![0; 10 * 1024 * 1024 + 1024]).file_name("a.png");
    let rejected = put_photo(&mut client, patient_id, over_the_body_limit).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(
        rejected.errors(),
        [("generalErrors".to_owned(), "The request body contains malformed form data!".to_owned())]
    );

    let not_a_form = client.put(&format!("/patients/{patient_id}/photo"), json!({})).await;
    assert_eq!(not_a_form.status, StatusCode::UNSUPPORTED_MEDIA_TYPE);

    let part = reqwest::multipart::Part::bytes(png()).file_name("a.png");
    let missing = put_photo(&mut client, 99999999, part).await.expect(StatusCode::NOT_FOUND);
    assert_eq!(missing.detail(), "Patient was not found.");

    assert_eq!(client.get(&format!("/patients/{patient_id}")).await.ok().json()["hasPhoto"], false);
}

//! Face endpoints, with the real models and enrolled faces kept in memory.
//!
//! The index is shared by every test (they run in parallel against one server), so the
//! same face may be enrolled under several people at once: searches ask for many matches
//! and look for their own person among them.
//!
//! Tests that need the models skip themselves when the model files are not installed.

use std::io::Cursor;

use reqwest::multipart::{Form, Part};
use reqwest::{Method, StatusCode};
use sabai_api::config::FaceRecognitionSettings;
use serde_json::Value;

use crate::support::*;

const SAME_PERSON_A: &str = "obama-2012.jpg";
const SAME_PERSON_B: &str = "obama-2009.jpg";
const OTHER_PERSON: &str = "biden-2021.jpg";
const NO_FACE: &str = "No usable face detected. Use a closer, sharp, well-lit photo.";

/// Whether the model files are where the server looks for them; says so when not.
fn models_installed() -> bool {
    let settings = FaceRecognitionSettings::default();
    let installed = settings.detector_path().is_file() && settings.recognizer_path().is_file();
    if !installed {
        eprintln!("skipped: the face models are not in {}", settings.models_directory.display());
    }
    installed
}

fn photo(name: &str) -> Vec<u8> {
    let path = format!("{}/tests/fixtures/faces/{name}", env!("CARGO_MANIFEST_DIR"));
    std::fs::read(&path).unwrap_or_else(|error| panic!("reading {path}: {error}"))
}

/// A plain grey picture: a valid image with nobody in it.
fn empty_picture() -> Vec<u8> {
    let picture = image::RgbImage::from_pixel(96, 64, image::Rgb([120, 120, 120]));
    let mut png = Vec::new();
    picture.write_to(&mut Cursor::new(&mut png), image::ImageFormat::Png).unwrap();
    png
}

fn upload(field: &str, bytes: Vec<u8>) -> Form {
    Form::new().part(field.to_owned(), Part::bytes(bytes).file_name("photo"))
}

fn pair(first: Vec<u8>, second: Vec<u8>) -> Form {
    upload("first", first).part("second", Part::bytes(second).file_name("photo"))
}

fn errors(response: &Response) -> Vec<(String, String)> {
    response.errors()
}

fn error(name: &str, reason: &str) -> (String, String) {
    (name.to_owned(), reason.to_owned())
}

fn near(value: &Value, expected: f64, tolerance: f64) -> bool {
    value.as_f64().is_some_and(|value| (value - expected).abs() <= tolerance)
}

async fn enroll_person(client: &mut Client, person: &str, name: &str) -> Value {
    client
        .post_multipart(&format!("/persons/{person}/faces"), upload("image", photo(name)))
        .await
        .expect(StatusCode::CREATED)
        .json()
}

async fn latest_face_id(patient: i64) -> Option<String> {
    sqlx::query_scalar(r#"SELECT "RekognitionFaceId" FROM "Patients" WHERE "Id" = $1"#)
        .bind(patient as i32)
        .fetch_one(&db().await)
        .await
        .unwrap()
}

#[tokio::test]
async fn detect_returns_box_score_and_landmarks_of_each_face() {
    if !models_installed() {
        return;
    }
    let mut client = Client::admin().await;

    let found = client.post_multipart("/faces/detect", upload("image", photo(SAME_PERSON_A))).await.expect(StatusCode::OK);
    let body = found.json();

    assert_eq!(body["width"], 960);
    assert_eq!(body["height"], 1199);
    let faces = body["faces"].as_array().unwrap();
    assert_eq!(faces.len(), 1);
    // What the previous API returned for this photo, give or take decoding differences.
    let face = &faces[0];
    assert!(near(&face["score"], 0.9032, 0.01), "{face}");
    for (key, expected) in [("x", 358.89), ("y", 80.45), ("width", 224.43), ("height", 330.57)] {
        assert!(near(&face["box"][key], expected, 2.0), "box {key}: {face}");
    }
    let landmarks = [(425.15, 204.19), (525.89, 204.19), (479.34, 266.06), (417.98, 302.70), (531.03, 303.21)];
    assert_eq!(face["landmarks"].as_array().unwrap().len(), 5);
    for (landmark, (x, y)) in face["landmarks"].as_array().unwrap().iter().zip(landmarks) {
        assert!(near(&landmark["x"], x, 2.0) && near(&landmark["y"], y, 2.0), "{landmark} is not near ({x}, {y})");
    }
}

#[tokio::test]
async fn detect_finds_nobody_in_an_empty_picture() {
    if !models_installed() {
        return;
    }
    let mut client = Client::admin().await;

    let found = client.post_multipart("/faces/detect", upload("image", empty_picture())).await.expect(StatusCode::OK);

    assert_eq!(found.json(), serde_json::json!({ "width": 96, "height": 64, "faces": [] }));
}

#[tokio::test]
async fn uploads_must_be_readable_images_sent_as_multipart() {
    let mut client = Client::admin().await;

    let garbage = client.post_multipart("/faces/detect", upload("image", b"not an image".to_vec())).await;
    let garbage = garbage.expect(StatusCode::BAD_REQUEST);
    assert_eq!(errors(&garbage), [error("image", "Unsupported or corrupt image.")]);
    assert_eq!(garbage.json()["detail"], "Unsupported or corrupt image.");

    for form in [upload("image", Vec::new()), upload("other", photo(SAME_PERSON_A))] {
        let missing = client.post_multipart("/faces/detect", form).await.expect(StatusCode::BAD_REQUEST);
        assert_eq!(errors(&missing), [error("image", "An image file is required.")]);
    }

    let text = client.post_multipart("/faces/detect", Form::new().text("image", "hello")).await;
    let text = text.expect(StatusCode::BAD_REQUEST);
    assert_eq!(errors(&text), [error("image", "Value [hello] is not valid for a [IFormFile] property!")]);

    client.post_empty("/faces/detect").await.expect(StatusCode::UNSUPPORTED_MEDIA_TYPE);
    client.post("/faces/verify", serde_json::json!({})).await.expect(StatusCode::UNSUPPORTED_MEDIA_TYPE);
}

#[tokio::test]
async fn face_endpoints_need_a_staff_session() {
    let mut anonymous = Client::anonymous();
    for path in ["/faces/detect", "/faces/identify", "/faces/verify", "/persons/p/faces", "/patients/1/faces", "/patients/search-by-face"] {
        anonymous.post_multipart(path, upload("image", Vec::new())).await.expect(StatusCode::UNAUTHORIZED);
    }
    for path in ["/persons/p/faces", "/patients/1/faces"] {
        anonymous.get(path).await.expect(StatusCode::UNAUTHORIZED);
    }
    for path in ["/persons/p", "/faces/00000000-0000-0000-0000-000000000000", "/patients/1/faces/00000000-0000-0000-0000-000000000000"] {
        anonymous.delete(path).await.expect(StatusCode::UNAUTHORIZED);
    }
}

#[tokio::test]
async fn verify_matches_the_same_person_and_not_different_people() {
    if !models_installed() {
        return;
    }
    let mut client = Client::admin().await;

    let same = client.post_multipart("/faces/verify", pair(photo(SAME_PERSON_A), photo(SAME_PERSON_B))).await;
    let same = same.expect(StatusCode::OK).json();
    // The previous API scored this pair 0.7507.
    assert!(near(&same["similarity"], 0.7507, 0.02), "{same}");
    assert_eq!(same["isMatch"], true);
    assert!(near(&same["threshold"], 0.4, 1e-6));
    assert!(near(&same["first"]["box"]["x"], 358.89, 2.0));
    assert!(near(&same["second"]["box"]["x"], 393.64, 2.0));
    assert_eq!(same["second"]["landmarks"].as_array().unwrap().len(), 5);

    let different = client.post_multipart("/faces/verify", pair(photo(SAME_PERSON_A), photo(OTHER_PERSON))).await;
    let different = different.expect(StatusCode::OK).json();
    // The previous API scored this pair -0.0722.
    assert!(near(&different["similarity"], -0.0722, 0.02), "{different}");
    assert_eq!(different["isMatch"], false);
}

#[tokio::test]
async fn verify_says_which_photo_has_no_usable_face() {
    if !models_installed() {
        return;
    }
    let mut client = Client::admin().await;

    let neither = client.post_multipart("/faces/verify", pair(empty_picture(), empty_picture())).await;
    let neither = neither.expect(StatusCode::UNPROCESSABLE_ENTITY);
    assert_eq!(errors(&neither), [error("first", NO_FACE), error("second", NO_FACE)]);
    assert_eq!(neither.json()["title"], "Unprocessable Entity");
    assert_eq!(neither.json()["status"], 422);
    assert_eq!(neither.json()["detail"], Value::Null);

    let second = client.post_multipart("/faces/verify", pair(photo(OTHER_PERSON), empty_picture())).await;
    let second = second.expect(StatusCode::UNPROCESSABLE_ENTITY);
    assert_eq!(errors(&second), [error("second", NO_FACE)]);
    assert_eq!(second.json()["detail"], NO_FACE);
}

#[tokio::test]
async fn verify_needs_two_readable_images() {
    let mut client = Client::admin().await;

    let one = client.post_multipart("/faces/verify", upload("first", empty_picture())).await;
    assert_eq!(errors(&one.expect(StatusCode::BAD_REQUEST)), [error("second", "An image file is required.")]);

    let bad = client.post_multipart("/faces/verify", pair(b"nope".to_vec(), b"nope".to_vec())).await;
    assert_eq!(
        errors(&bad.expect(StatusCode::BAD_REQUEST)),
        [error("first", "Unsupported or corrupt image."), error("second", "Unsupported or corrupt image.")]
    );
}

#[tokio::test]
async fn a_person_is_enrolled_listed_identified_and_removed() {
    if !models_installed() {
        return;
    }
    let mut client = Client::admin().await;
    let person = format!("person-{}", suffix());
    let faces_path = format!("/persons/{person}/faces");

    let enrolled = client.post_multipart(&faces_path, upload("image", photo(SAME_PERSON_A))).await;
    let enrolled = enrolled.expect(StatusCode::CREATED);
    assert_eq!(enrolled.header("location"), None);
    let first = enrolled.json();
    assert_eq!(first["personId"], person.as_str());
    assert_eq!(first["facesDetected"], 1);
    assert!(near(&first["face"]["box"]["x"], 358.89, 2.0), "{first}");
    let first_id = first["faceId"].as_str().unwrap().to_owned();
    assert!(uuid::Uuid::parse_str(&first_id).is_ok());

    // Another photo of the same person is recognised; someone else is not.
    let identified = client
        .post_multipart("/faces/identify?maxMatches=20", upload("image", photo(SAME_PERSON_B)))
        .await
        .expect(StatusCode::OK)
        .json();
    let faces = identified["faces"].as_array().unwrap();
    assert_eq!(faces.len(), 1);
    assert!(near(&faces[0]["face"]["box"]["x"], 393.64, 2.0));
    let matches = faces[0]["matches"].as_array().unwrap();
    let mine = matches.iter().find(|found| found["personId"] == person.as_str()).expect("the enrolled person matches");
    assert_eq!(mine["faceId"], first_id.as_str());
    assert!(near(&mine["similarity"], 0.7507, 0.02), "{mine}");
    let scores: Vec<f64> = matches.iter().map(|found| found["similarity"].as_f64().unwrap()).collect();
    assert!(scores.windows(2).all(|pair| pair[0] >= pair[1]), "most similar first: {scores:?}");

    let stranger = client
        .post_multipart("/faces/identify?maxMatches=20", upload("image", photo(OTHER_PERSON)))
        .await
        .expect(StatusCode::OK)
        .json();
    assert!(stranger["faces"][0]["matches"].as_array().unwrap().iter().all(|found| found["personId"] != person.as_str()));

    // Only matches at or above the requested threshold are returned.
    let strict = client
        .post_multipart("/faces/identify?maxMatches=20&threshold=0.9", upload("image", photo(SAME_PERSON_B)))
        .await
        .expect(StatusCode::OK)
        .json();
    let strict = strict["faces"][0]["matches"].as_array().unwrap();
    assert!(strict.iter().all(|found| found["personId"] != person.as_str() && found["similarity"].as_f64().unwrap() >= 0.9));

    let second = enroll_person(&mut client, &person, SAME_PERSON_B).await;
    let second_id = second["faceId"].as_str().unwrap().to_owned();
    let listed = client.get(&faces_path).await.expect(StatusCode::OK).json();
    assert_eq!(listed["personId"], person.as_str());
    let listed_faces = listed["faces"].as_array().unwrap();
    let ids: Vec<&str> = listed_faces.iter().map(|face| face["faceId"].as_str().unwrap()).collect();
    assert_eq!(ids, [first_id.as_str(), second_id.as_str()], "oldest first");
    assert!(listed_faces.iter().all(|face| face["personId"] == person.as_str() && face["createdAt"].is_string()));

    // One best match per person, whichever of their photos is closest.
    let again = client
        .post_multipart("/faces/identify?maxMatches=20", upload("image", photo(SAME_PERSON_B)))
        .await
        .expect(StatusCode::OK)
        .json();
    let mine: Vec<&Value> =
        again["faces"][0]["matches"].as_array().unwrap().iter().filter(|found| found["personId"] == person.as_str()).collect();
    assert_eq!(mine.len(), 1);
    assert_eq!(mine[0]["faceId"], second_id.as_str());
    assert!(near(&mine[0]["similarity"], 1.0, 1e-3));

    client.delete(&format!("/faces/{first_id}")).await.expect(StatusCode::NO_CONTENT);
    let gone = client.delete(&format!("/faces/{first_id}")).await.expect(StatusCode::NOT_FOUND);
    assert!(gone.bytes.is_empty());
    assert_eq!(client.get(&faces_path).await.ok().json()["faces"].as_array().unwrap().len(), 1);

    client.delete(&format!("/persons/{person}")).await.expect(StatusCode::NO_CONTENT);
    let gone = client.delete(&format!("/persons/{person}")).await.expect(StatusCode::NOT_FOUND);
    assert!(gone.bytes.is_empty());
    assert_eq!(client.get(&faces_path).await.ok().json(), serde_json::json!({ "personId": person, "faces": [] }));
}

#[tokio::test]
async fn unknown_people_and_faces_are_not_found() {
    if !models_installed() {
        return;
    }
    let mut client = Client::admin().await;
    let person = format!("nobody-{}", suffix());

    let listed = client.get(&format!("/persons/{person}/faces")).await.expect(StatusCode::OK).json();
    assert_eq!(listed, serde_json::json!({ "personId": person, "faces": [] }));
    client.delete(&format!("/persons/{person}")).await.expect(StatusCode::NOT_FOUND);
    client.delete(&format!("/faces/{}", uuid::Uuid::new_v4())).await.expect(StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn enrolling_a_picture_without_a_face_is_refused() {
    if !models_installed() {
        return;
    }
    let mut client = Client::admin().await;
    let person = format!("person-{}", suffix());

    let refused = client.post_multipart(&format!("/persons/{person}/faces"), upload("image", empty_picture())).await;
    let refused = refused.expect(StatusCode::UNPROCESSABLE_ENTITY);
    assert_eq!(errors(&refused), [error("image", NO_FACE)]);
    assert_eq!(refused.json()["detail"], NO_FACE);
    assert_eq!(refused.json()["type"], "https://www.rfc-editor.org/rfc/rfc4918#section-11.2");
    assert_eq!(client.get(&format!("/persons/{person}/faces")).await.ok().json()["faces"], serde_json::json!([]));
}

#[tokio::test]
async fn person_ids_and_face_ids_are_validated() {
    let mut client = Client::admin().await;
    let rule = error("personId", "PersonId must be 1-128 characters.");
    let long = "p".repeat(129);

    for person in ["%20", long.as_str()] {
        let listed = client.get(&format!("/persons/{person}/faces")).await.expect(StatusCode::BAD_REQUEST);
        assert_eq!(errors(&listed), std::slice::from_ref(&rule));
        let deleted = client.delete(&format!("/persons/{person}")).await.expect(StatusCode::BAD_REQUEST);
        assert_eq!(errors(&deleted), std::slice::from_ref(&rule));
    }
    let enrolled = client.post_multipart("/persons/%20/faces", upload("image", b"nope".to_vec())).await;
    assert_eq!(
        errors(&enrolled.expect(StatusCode::BAD_REQUEST)),
        [rule.clone(), error("image", "Unsupported or corrupt image.")]
    );

    let face = client.delete("/faces/not-a-guid").await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(errors(&face), [error("faceId", "Value [not-a-guid] is not valid for a [Guid] property!")]);
    let face = client.delete("/patients/1/faces/zzz").await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(errors(&face), [error("faceId", "Value [zzz] is not valid for a [Guid] property!")]);
}

#[tokio::test]
async fn search_options_are_read_from_the_query_and_validated() {
    let mut client = Client::admin().await;
    let threshold = error("threshold", "Threshold must be between -1 and 1.");
    let max_matches = error("maxMatches", "MaxMatches must be between 1 and 20.");

    for path in ["/faces/identify", "/patients/search-by-face"] {
        let invalid = client
            .post_multipart(&format!("{path}?threshold=2&maxMatches=0"), upload("image", b"nope".to_vec()))
            .await
            .expect(StatusCode::BAD_REQUEST);
        assert_eq!(
            errors(&invalid),
            [threshold.clone(), max_matches.clone(), error("image", "Unsupported or corrupt image.")]
        );
        assert_eq!(invalid.json()["detail"], Value::Null);

        let too_many = client
            .post_multipart(&format!("{path}?maxMatches=21&threshold=-1"), upload("image", empty_picture()))
            .await
            .expect(StatusCode::BAD_REQUEST);
        assert_eq!(errors(&too_many), std::slice::from_ref(&max_matches));

        let unreadable = client
            .post_multipart(&format!("{path}?threshold=abc"), upload("image", empty_picture()))
            .await
            .expect(StatusCode::BAD_REQUEST);
        assert_eq!(errors(&unreadable), [error("threshold", "Value [abc] is not valid for a [Single] property!")]);
    }
}

#[tokio::test]
async fn search_options_sent_as_form_fields_are_ignored() {
    if !models_installed() {
        return;
    }
    let mut client = Client::admin().await;

    for path in ["/faces/identify", "/patients/search-by-face"] {
        let form = upload("image", empty_picture()).text("maxMatches", "40").text("threshold", "7");
        client.post_multipart(path, form).await.expect(StatusCode::OK);
    }
}

#[tokio::test]
async fn a_patient_face_is_enrolled_found_and_removed() {
    if !models_installed() {
        return;
    }
    let mut client = Client::admin().await;
    let village = create_village(&mut client).await;
    let registered = register_patient(&mut client, id(&village)).await;
    let patient = id(&registered);
    let faces_path = format!("/patients/{patient}/faces");
    assert_eq!(client.get(&faces_path).await.expect(StatusCode::OK).json(), serde_json::json!({ "patientId": patient, "faces": [] }));

    let enrolled = client.post_multipart(&faces_path, upload("image", photo(SAME_PERSON_A))).await;
    let enrolled = enrolled.expect(StatusCode::CREATED);
    assert_eq!(enrolled.header("location"), None);
    let first = enrolled.json();
    assert_eq!(first["patientId"], patient);
    assert_eq!(first["facesDetected"], 1);
    assert!(near(&first["face"]["score"], 0.9032, 0.01), "{first}");
    let first_id = first["faceId"].as_str().unwrap().to_owned();
    assert_eq!(latest_face_id(patient).await.as_deref(), Some(first_id.as_str()));

    // The change to the patient is in the audit log; the face itself lives in the face store.
    let log = client.get(&format!("/audit-logs?entityType=Patient&entityId={patient}")).await.ok().json();
    let entry = &log["entries"][0];
    assert_eq!(entry["action"], "Updated");
    assert_eq!(entry["request"], format!("POST {faces_path}"));
    assert_eq!(entry["changes"], serde_json::json!([{ "field": "LatestFaceId", "old": null, "new": first_id }]));

    let listed = client.get(&faces_path).await.expect(StatusCode::OK).json();
    assert_eq!(listed["patientId"], patient);
    assert_eq!(listed["faces"].as_array().unwrap().len(), 1);
    assert_eq!(listed["faces"][0]["faceId"], first_id.as_str());
    assert_eq!(listed["faces"][0]["personId"], format!("patient:{patient}"));

    // Another photo of the same person finds the patient; someone else's does not.
    let found = client
        .post_multipart("/patients/search-by-face?maxMatches=20", upload("image", photo(SAME_PERSON_B)))
        .await
        .expect(StatusCode::OK)
        .json();
    let matches = found["matches"].as_array().unwrap();
    let mine = matches.iter().find(|found| found["id"] == patient).expect("the enrolled patient is found");
    let code = village["code"].as_str().unwrap();
    assert_eq!(mine["name"], registered["name"]);
    assert_eq!(mine["displayId"], format!("{code}{patient:04}"));
    assert_eq!(mine["villageCode"], code);
    assert_eq!(mine["villageColorHex"], "#336699");
    assert_eq!(mine["hasPhoto"], false);
    assert!(near(&mine["similarity"], 0.7507, 0.02), "{mine}");
    let scores: Vec<f64> = matches.iter().map(|found| found["similarity"].as_f64().unwrap()).collect();
    assert!(scores.windows(2).all(|pair| pair[0] >= pair[1]), "most similar first: {scores:?}");

    let stranger = client
        .post_multipart("/patients/search-by-face?maxMatches=20", upload("image", photo(OTHER_PERSON)))
        .await
        .expect(StatusCode::OK)
        .json();
    assert!(stranger["matches"].as_array().unwrap().iter().all(|found| found["id"] != patient));
    let nobody = client.post_multipart("/patients/search-by-face", upload("image", empty_picture())).await;
    assert_eq!(nobody.expect(StatusCode::OK).json(), serde_json::json!({ "matches": [] }));

    // A second photo becomes the latest; removing it falls back to the one before.
    let second = client.post_multipart(&faces_path, upload("image", photo(SAME_PERSON_B))).await;
    let second_id = second.expect(StatusCode::CREATED).json()["faceId"].as_str().unwrap().to_owned();
    assert_eq!(latest_face_id(patient).await.as_deref(), Some(second_id.as_str()));
    let ids: Vec<String> = client.get(&faces_path).await.ok().json()["faces"]
        .as_array()
        .unwrap()
        .iter()
        .map(|face| face["faceId"].as_str().unwrap().to_owned())
        .collect();
    assert_eq!(ids, [first_id.clone(), second_id.clone()], "oldest first");

    client.delete(&format!("{faces_path}/{second_id}")).await.expect(StatusCode::NO_CONTENT);
    assert_eq!(latest_face_id(patient).await.as_deref(), Some(first_id.as_str()));
    let again = client.delete(&format!("{faces_path}/{second_id}")).await.expect(StatusCode::NOT_FOUND);
    assert_eq!(again.detail(), "Face was not found for this patient.");

    client.delete(&format!("{faces_path}/{first_id}")).await.expect(StatusCode::NO_CONTENT);
    assert_eq!(latest_face_id(patient).await, None);
    assert_eq!(client.get(&faces_path).await.ok().json()["faces"], serde_json::json!([]));
}

#[tokio::test]
async fn removing_an_older_patient_face_keeps_the_latest() {
    if !models_installed() {
        return;
    }
    let mut client = Client::admin().await;
    let (patient, _) = patient_with_visit(&mut client).await;
    let faces_path = format!("/patients/{patient}/faces");
    let mut ids = Vec::new();
    for name in [SAME_PERSON_A, SAME_PERSON_B] {
        let enrolled = client.post_multipart(&faces_path, upload("image", photo(name))).await.expect(StatusCode::CREATED);
        ids.push(enrolled.json()["faceId"].as_str().unwrap().to_owned());
    }

    client.delete(&format!("{faces_path}/{}", ids[0])).await.expect(StatusCode::NO_CONTENT);

    assert_eq!(latest_face_id(patient).await.as_deref(), Some(ids[1].as_str()));
}

#[tokio::test]
async fn a_patient_only_gives_access_to_their_own_faces() {
    if !models_installed() {
        return;
    }
    let mut client = Client::admin().await;
    let (patient, _) = patient_with_visit(&mut client).await;
    let (other_patient, _) = patient_with_visit(&mut client).await;
    let person = format!("person-{}", suffix());
    let person_face = enroll_person(&mut client, &person, OTHER_PERSON).await["faceId"].as_str().unwrap().to_owned();
    let other_face = client
        .post_multipart(&format!("/patients/{other_patient}/faces"), upload("image", photo(OTHER_PERSON)))
        .await
        .expect(StatusCode::CREATED)
        .json()["faceId"]
        .as_str()
        .unwrap()
        .to_owned();

    for face in [&person_face, &other_face] {
        let refused = client.delete(&format!("/patients/{patient}/faces/{face}")).await.expect(StatusCode::NOT_FOUND);
        assert_eq!(refused.detail(), "Face was not found for this patient.");
    }

    // Faces enrolled under a plain person id are never patients.
    let found = client
        .post_multipart("/patients/search-by-face?maxMatches=20", upload("image", photo(OTHER_PERSON)))
        .await
        .expect(StatusCode::OK)
        .json();
    let found: Vec<i64> = found["matches"].as_array().unwrap().iter().map(id).collect();
    assert!(found.contains(&other_patient) && !found.contains(&patient), "{found:?}");
    assert_eq!(client.get(&format!("/persons/{person}/faces")).await.ok().json()["faces"].as_array().unwrap().len(), 1);
    assert_eq!(latest_face_id(other_patient).await.as_deref(), Some(other_face.as_str()));
}

#[tokio::test]
async fn patient_face_endpoints_check_the_patient_first() {
    let mut client = Client::admin().await;
    let missing = "/patients/99999999/faces";

    for response in [
        client.get(missing).await,
        client.post_multipart(missing, upload("image", b"nope".to_vec())).await,
        client.delete(&format!("{missing}/{}", uuid::Uuid::new_v4())).await,
    ] {
        assert_eq!(response.expect(StatusCode::NOT_FOUND).detail(), "Patient was not found.");
    }

    let unreadable = client.post_multipart("/patients/abc/faces", upload("image", empty_picture())).await;
    assert_eq!(
        errors(&unreadable.expect(StatusCode::BAD_REQUEST)),
        [error("id", "Value [abc] is not valid for a [Int32] property!")]
    );

    let (patient, _) = patient_with_visit(&mut client).await;
    let bad = client.post_multipart(&format!("/patients/{patient}/faces"), upload("image", b"nope".to_vec())).await;
    assert_eq!(errors(&bad.expect(StatusCode::BAD_REQUEST)), [error("image", "Unsupported or corrupt image.")]);
    assert_eq!(latest_face_id(patient).await, None);
}

#[tokio::test]
async fn enrolling_a_patient_picture_without_a_face_is_refused() {
    if !models_installed() {
        return;
    }
    let mut client = Client::admin().await;
    let (patient, _) = patient_with_visit(&mut client).await;

    let refused = client.post_multipart(&format!("/patients/{patient}/faces"), upload("image", empty_picture())).await;

    assert_eq!(errors(&refused.expect(StatusCode::UNPROCESSABLE_ENTITY)), [error("image", NO_FACE)]);
    assert_eq!(latest_face_id(patient).await, None);
}

#[tokio::test]
async fn members_may_use_the_face_endpoints() {
    let mut member = Client::signed_in(REGISTRATION).await;
    let response = member.send(member.request(Method::GET, "/patients/99999999/faces")).await;
    assert_eq!(response.expect(StatusCode::NOT_FOUND).detail(), "Patient was not found.");
}

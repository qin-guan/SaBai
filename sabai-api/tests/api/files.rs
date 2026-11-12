use reqwest::StatusCode;
use reqwest::multipart::{Form, Part};
use serde_json::{Value, json};

use crate::support::*;

const PDF: &str = "application/pdf";

fn file_part(text: &str, file_name: &str, content_type: &str) -> Part {
    Part::bytes(text.as_bytes().to_vec()).file_name(file_name.to_owned()).mime_str(content_type).unwrap()
}

/// One PDF containing "clinic note" for the patient.
fn form(patient_id: i64, file_name: &str, description: &str) -> Form {
    Form::new()
        .text("patientId", patient_id.to_string())
        .part("files", file_part("clinic note", file_name, PDF))
        .text("descriptions", description.to_owned())
}

async fn create_patient(client: &mut Client) -> i64 {
    patient_with_visit(client).await.0
}

async fn upload(client: &mut Client, patient_id: i64, file_name: &str, description: &str) -> i64 {
    let created = client.post_multipart("/files", form(patient_id, file_name, description)).await.ok().json();
    id(&created[0])
}

async fn listed(client: &mut Client, query: &str) -> Vec<Value> {
    client.get(&format!("/files?{query}")).await.expect(StatusCode::OK).json().as_array().expect("a list of files").clone()
}

fn names(files: &[Value]) -> Vec<&str> {
    files.iter().map(|file| file["fileName"].as_str().unwrap()).collect()
}

#[tokio::test]
async fn uploaded_file_is_hidden_by_a_soft_delete_until_restored() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let patient_id = create_patient(&mut client).await;
    let file_name = format!("note-{}.pdf", suffix());

    let uploaded = client.post_multipart("/files", form(patient_id, &file_name, "visit note")).await.expect(StatusCode::CREATED);
    let created = uploaded.json();
    let file_id = id(&created[0]);
    let content_path = format!("/files/{file_id}/content");
    assert!(uploaded.text().trim_start().starts_with('['));
    assert_eq!(uploaded.header("location"), Some(format!("/files/{file_id}").as_str()));
    assert_eq!(created[0]["fileName"], file_name);
    assert_eq!(created[0]["patientId"], patient_id);
    assert_eq!(created[0]["description"], "visit note");
    assert_eq!(created[0]["sizeBytes"], 11);
    assert_eq!(created[0]["isDeleted"], false);
    let stored_name = created[0]["storedFileName"].as_str().unwrap();
    assert!(stored_name.len() == 36 && stored_name.ends_with(".pdf"), "{stored_name}");

    let downloaded = client.get(&content_path).await.expect(StatusCode::OK);
    assert_eq!(downloaded.text(), "clinic note");
    assert_eq!(downloaded.header("content-type"), Some(PDF));
    assert_eq!(downloaded.header("content-length"), Some("11"));
    assert_eq!(
        downloaded.header("content-disposition"),
        Some(format!("attachment; filename={file_name}; filename*=UTF-8''{file_name}").as_str())
    );
    assert_eq!(names(&listed(&mut client, &format!("patientId={patient_id}")).await), [file_name.as_str()]);

    let deleted = client.delete(&format!("/files/{file_id}")).await.expect(StatusCode::OK).json();
    assert_eq!(deleted["isDeleted"], true);
    assert!(listed(&mut client, &format!("patientId={patient_id}")).await.is_empty());
    let hidden = client.get(&content_path).await.expect(StatusCode::NOT_FOUND);
    assert_eq!(hidden.detail(), "File was not found.");

    let restored = client.post_empty(&format!("/files/{file_id}/restore")).await.expect(StatusCode::OK).json();
    assert_eq!(restored["isDeleted"], false);
    assert_eq!(names(&listed(&mut client, &format!("patientId={patient_id}")).await), [file_name.as_str()]);
    let restored_content = client.get(&content_path).await.expect(StatusCode::OK);
    assert_eq!(restored_content.text(), "clinic note");
}

#[tokio::test]
async fn upload_without_a_file_is_rejected() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let patient_id = create_patient(&mut client).await;
    let form = Form::new().text("patientId", patient_id.to_string()).text("descriptions", "missing file");

    let response = client.post_multipart("/files", form).await.expect(StatusCode::BAD_REQUEST);

    assert_eq!(response.detail(), "No file was uploaded.");
}

#[tokio::test]
async fn upload_for_an_unknown_patient_is_rejected() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let file_name = format!("orphan-{}.pdf", suffix());

    let response = client.post_multipart("/files", form(99999999, &file_name, "orphan note")).await.expect(StatusCode::BAD_REQUEST);

    assert_eq!(response.detail(), "Patient was not found.");
}

#[tokio::test]
async fn upload_needs_one_description_per_file() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let patient_id = create_patient(&mut client).await;
    let suffix = suffix();
    let form = Form::new()
        .text("patientId", patient_id.to_string())
        .part("files", file_part("one", &format!("a-{suffix}.pdf"), PDF))
        .part("files", file_part("two", &format!("b-{suffix}.pdf"), PDF))
        .text("descriptions", "only one");

    let response = client.post_multipart("/files", form).await.expect(StatusCode::BAD_REQUEST);

    assert_eq!(response.detail(), "Number of files and descriptions must match.");
}

#[tokio::test]
async fn list_rejects_an_unknown_deleted_filter() {
    let mut client = Client::signed_in(PHARMACIST).await;

    let response = client.get("/files?deleted=maybe").await.expect(StatusCode::BAD_REQUEST);

    assert_eq!(response.detail(), "deleted parameter must be 'all', 'true', or 'false'.");
}

#[tokio::test]
async fn unknown_file_is_not_found() {
    let mut client = Client::signed_in(PHARMACIST).await;

    let response = client.get("/files/99999999").await.expect(StatusCode::NOT_FOUND);

    assert_eq!(response.detail(), "File was not found.");
    client.get("/files/99999999/content").await.expect(StatusCode::NOT_FOUND);
    client.delete("/files/99999999").await.expect(StatusCode::NOT_FOUND);
    client.post_empty("/files/99999999/restore").await.expect(StatusCode::NOT_FOUND);
    client.patch("/files/99999999", json!({})).await.expect(StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn download_requires_a_session() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let patient_id = create_patient(&mut client).await;
    let file_id = upload(&mut client, patient_id, &format!("secret-{}.pdf", suffix()), "private").await;
    let mut anonymous = Client::anonymous();

    let response = anonymous.get(&format!("/files/{file_id}/content")).await;

    assert_eq!(response.status, StatusCode::UNAUTHORIZED);
}

#[tokio::test]
async fn updated_description_is_returned_on_get() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let suffix = suffix();
    let patient_id = create_patient(&mut client).await;
    let file_id = upload(&mut client, patient_id, &format!("note-{suffix}.pdf"), "original note").await;
    let description = format!("Updated note {suffix}");
    let path = format!("/files/{file_id}");

    let patched = client.patch(&path, json!({ "description": description })).await.expect(StatusCode::OK).json();
    let fetched = client.get(&path).await.expect(StatusCode::OK).json();

    assert_eq!(fetched["description"], description);
    assert_eq!(fetched, patched);
}

#[tokio::test]
async fn file_can_be_renamed_to_a_bare_name() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let patient_id = create_patient(&mut client).await;
    let file_id = upload(&mut client, patient_id, "before.pdf", "note").await;
    let path = format!("/files/{file_id}");

    let renamed = client.patch(&path, json!({ "fileName": " a/b/renamed.txt ", "description": "" })).await.ok().json();
    assert_eq!(renamed["fileName"], "renamed.txt");
    assert_eq!(renamed["description"], "");
    assert!(renamed["storedFileName"].as_str().unwrap().ends_with(".pdf"));

    let blank = client.patch(&path, json!({ "fileName": "  a/ " })).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(blank.detail(), "No name provided.");
    let long = client.patch(&path, json!({ "fileName": "x".repeat(256) })).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(
        long.errors(),
        [(
            "fileName".to_owned(),
            "The length of 'file Name' must be 255 characters or fewer. You entered 256 characters.".to_owned()
        )]
    );
    let zero = client.patch("/files/0", json!({})).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(zero.errors(), [("id".to_owned(), "'id' must be greater than '0'.".to_owned())]);
}

#[tokio::test]
async fn deleted_and_all_filters_contain_a_soft_deleted_file() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let patient_id = create_patient(&mut client).await;
    let file_name = format!("deleted-{}.pdf", suffix());
    let kept_name = format!("kept-{}.pdf", suffix());
    let file_id = upload(&mut client, patient_id, &file_name, "to delete").await;
    upload(&mut client, patient_id, &kept_name, "to keep").await;
    client.delete(&format!("/files/{file_id}")).await.ok();

    let only_deleted = listed(&mut client, &format!("patientId={patient_id}&deleted=%20TRUE%20")).await;
    let all = listed(&mut client, &format!("patientId={patient_id}&deleted=all")).await;
    let current = listed(&mut client, &format!("patientId={patient_id}&deleted=false")).await;

    assert_eq!(names(&only_deleted), [file_name.as_str()]);
    assert_eq!(names(&all), [kept_name.as_str(), file_name.as_str()], "newest first");
    assert_eq!(names(&current), [kept_name.as_str()]);
}

#[tokio::test]
async fn upload_of_a_disallowed_type_or_an_empty_file_is_rejected() {
    let mut client = Client::signed_in(PHARMACIST).await;
    for (content_type, body, reason) in [
        ("text/plain", "clinic note", "Only PDF, JPEG, PNG and DOCX files are allowed."),
        (PDF, "", "File is empty."),
    ] {
        let patient_id = create_patient(&mut client).await;
        let file_name = format!("rejected-{}.bin", suffix());
        let form = Form::new()
            .text("patientId", patient_id.to_string())
            .part("files", file_part(body, &file_name, content_type))
            .text("descriptions", "rejected");

        let response = client.post_multipart("/files", form).await.expect(StatusCode::BAD_REQUEST);

        assert_eq!(response.detail(), format!("{file_name} - {reason}"));
        assert!(listed(&mut client, &format!("patientId={patient_id}&deleted=all")).await.is_empty());
    }
}

#[tokio::test]
async fn several_files_are_stored_together_or_not_at_all() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let patient_id = create_patient(&mut client).await;

    // Field names in any case; the directory part of a name is dropped.
    let form = Form::new()
        .text("PatientId", patient_id.to_string())
        .part("Files", file_part("one", "dir/one.final.pdf", "APPLICATION/PDF"))
        .part("Files", file_part("two!", "noext", "image/png"))
        .text("Descriptions", "")
        .text("Descriptions", "second");
    let uploaded = client.post_multipart("/files", form).await.expect(StatusCode::CREATED);
    let created = uploaded.json();
    assert_eq!(uploaded.header("location"), Some(format!("/files/{}", id(&created[0])).as_str()));
    assert_eq!(created[0]["fileName"], "one.final.pdf");
    assert_eq!(created[0]["description"], "");
    assert!(created[0]["storedFileName"].as_str().unwrap().ends_with(".pdf"));
    assert_eq!(created[1]["fileName"], "noext");
    assert_eq!(created[1]["description"], "second");
    assert_eq!(created[1]["sizeBytes"], 4);
    assert_eq!(created[1]["storedFileName"].as_str().unwrap().len(), 32);
    let second = client.get(&format!("/files/{}/content", id(&created[1]))).await.expect(StatusCode::OK);
    assert_eq!(second.header("content-type"), Some("image/png"));
    assert_eq!(second.text(), "two!");

    // The second file is refused, so the first is not kept either.
    let other_patient = create_patient(&mut client).await;
    let form = Form::new()
        .text("patientId", other_patient.to_string())
        .part("files", file_part("ok", "ok.pdf", PDF))
        .part("files", file_part("bad", "bad.txt", "text/plain"))
        .text("descriptions", "a")
        .text("descriptions", "b");
    let refused = client.post_multipart("/files", form).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(refused.detail(), "bad.txt - Only PDF, JPEG, PNG and DOCX files are allowed.");
    assert!(listed(&mut client, &format!("patientId={other_patient}&deleted=all")).await.is_empty());

    // Files sent under another field name are taken when none is named `files`.
    let form = Form::new()
        .text("descriptions", "by another name")
        .part("document", file_part("doc", "rapport.pdf", PDF));
    let uploaded = client.post_multipart(&format!("/files?patientId={other_patient}"), form).await.expect(StatusCode::CREATED);
    let path = format!("/files/{}", id(&uploaded.json()[0]));
    client.patch(&path, json!({ "fileName": "rapport médical.pdf" })).await.ok();
    let download = client.get(&format!("{path}/content")).await.expect(StatusCode::OK);
    assert_eq!(
        download.headers.get("content-disposition").map(|value| value.as_bytes()),
        Some("attachment; filename=\"rapport m_dical.pdf\"; filename*=UTF-8''rapport%20m%C3%A9dical.pdf".as_bytes())
    );
}

#[tokio::test]
async fn upload_must_be_a_readable_multipart_form() {
    let mut client = Client::signed_in(PHARMACIST).await;
    let patient_id = create_patient(&mut client).await;

    let json_body = client.post("/files", json!({ "patientId": patient_id })).await;
    assert_eq!(json_body.status, StatusCode::UNSUPPORTED_MEDIA_TYPE);
    let no_body = client.post_empty("/files").await;
    assert_eq!(no_body.status, StatusCode::UNSUPPORTED_MEDIA_TYPE);

    let form = Form::new().text("patientId", "abc").part("files", file_part("x", "x.pdf", PDF)).text("descriptions", "d");
    let not_a_number = client.post_multipart("/files", form).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(
        not_a_number.errors(),
        [("patientId".to_owned(), "Value [abc] is not valid for a [Int32] property!".to_owned())]
    );

    let form = Form::new().part("files", file_part("x", "x.pdf", PDF)).text("descriptions", "d");
    let no_patient = client.post_multipart("/files", form).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(no_patient.detail(), "Patient was not found.");

    let form = Form::new()
        .text("patientId", patient_id.to_string())
        .part("files", file_part("x", &"x".repeat(256), PDF))
        .text("descriptions", "d");
    let long_name = client.post_multipart("/files", form).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(long_name.detail(), "File name is too long.");

    let form = Form::new()
        .text("patientId", patient_id.to_string())
        .part("files", Part::bytes(vec![0; 26_214_401]).file_name("big.pdf").mime_str(PDF).unwrap())
        .text("descriptions", "d");
    let too_large = client.post_multipart("/files", form).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(too_large.detail(), "big.pdf - File is too large.");

    assert!(listed(&mut client, &format!("patientId={patient_id}&deleted=all")).await.is_empty());
}

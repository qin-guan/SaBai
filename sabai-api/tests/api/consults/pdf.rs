use reqwest::StatusCode;
use serde_json::json;

use super::*;

/// The pages of the report as text, with runs of spaces collapsed; `None` (and a note in
/// the test output) on a machine without `pdftotext`.
fn report_pages(pdf: &[u8]) -> Option<Vec<String>> {
    let pages = pdf_pages(pdf).map(|pages| pages.iter().map(|page| squeeze(page)).collect());
    if pages.is_none() {
        eprintln!("pdftotext is not installed: the report's text was not checked");
    }
    pages
}

#[tokio::test]
async fn consult_pdf_is_an_attachment_named_after_the_consult_and_patient() {
    let mut client = Client::signed_in(DOCTOR).await;
    let code = format!("K.é-{}", suffix());
    let village = create_village_named(&mut client, &code, "Named").await;
    let patient = register_patient(&mut client, id(&village)).await;
    let consult = create_consult(&mut client, patient["visitId"].as_i64().unwrap()).await;

    let pdf = client.get(&format!("/consults/{}/pdf", id(&consult))).await.expect(StatusCode::OK);

    // Everything but ASCII letters and digits in the patient's display id becomes an underscore.
    let safe_code: String = code.chars().map(|ch| if ch.is_ascii_alphanumeric() { ch } else { '_' }).collect();
    let file_name = format!("consultation{}_report_{safe_code}{:04}.pdf", id(&consult), id(&patient));
    assert_eq!(pdf.header("content-type"), Some("application/pdf"));
    assert_eq!(
        pdf.header("content-disposition"),
        Some(format!("attachment; filename={file_name}; filename*=UTF-8''{file_name}").as_str())
    );
    assert_eq!(pdf.header("content-length"), Some(pdf.bytes.len().to_string().as_str()));
    assert!(pdf.bytes.starts_with(b"%PDF-"));
    assert!(pdf.bytes.trim_ascii_end().ends_with(b"%%EOF"));
    assert_eq!(page_count(&pdf.bytes), 1);
}

#[tokio::test]
async fn consult_pdf_of_a_missing_consult_is_not_found() {
    let mut client = Client::admin().await;

    let missing = client.get("/consults/99999999/pdf").await.expect(StatusCode::NOT_FOUND);
    assert_eq!(
        missing.json(),
        json!({ "title": "Not Found", "status": 404, "detail": "Consult was not found.", "instance": "/consults/99999999/pdf" })
    );
    let not_a_number = client.get("/consults/abc/pdf").await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(not_a_number.errors(), [("id".to_owned(), "Value [abc] is not valid for a [Int32] property!".to_owned())]);
}

#[tokio::test]
async fn consult_pdf_prints_the_report_with_markup_characters_and_khmer_text_as_written() {
    let mut admin = Client::admin().await;
    let mut doctor = Client::signed_in(DOCTOR).await;
    let village = create_village(&mut admin).await;
    let name = format!("សុខ តារា #strong[x] *{}* $y$", suffix());
    let patient = register_patient_named(&mut admin, id(&village), &name).await;
    let consultation = "#import \"x\": *bold* _it_ [link] `raw` <label> @ref \\ // not a comment\n= heading? សុខ តារា";
    let consult = doctor
        .post(
            "/consults",
            json!({
                "visitId": patient["visitId"],
                "pastMedicalHistory": "asthma & eczema",
                "consultation": consultation,
                "treatmentPlan": "   ",
                "remarks": "review in 2 weeks",
                "diagnoses": [
                    { "details": "otitis #1", "category": "Ear Nose Throat" },
                    { "details": "rash" },
                    { "details": "UTI", "category": "Renal & Genitourinary" },
                ],
            }),
        )
        .await
        .ok()
        .json();

    let pdf = admin.get(&format!("/consults/{}/pdf", id(&consult))).await.expect(StatusCode::OK);

    assert!(pdf.bytes.starts_with(b"%PDF-"));
    assert_eq!(page_count(&pdf.bytes), 1);
    let Some(pages) = report_pages(&pdf.bytes) else { return };
    assert_eq!(pages.len(), 1);
    let display_id = patient["displayId"].as_str().unwrap();
    let (first_line, second_line) = consultation.split_once('\n').unwrap();
    let expected = [
        "Consultation report".to_owned(),
        format!("{display_id} · {name}"),
        "Doctor: Doctor".to_owned(),
        "Past medical history".to_owned(),
        "asthma & eczema".to_owned(),
        "Consultation".to_owned(),
        first_line.to_owned(),
        second_line.to_owned(),
        "Diagnoses".to_owned(),
        "Ear Nose Throat: otitis #1".to_owned(),
        "Uncategorised: rash".to_owned(),
        "Renal & Genitourinary: UTI".to_owned(),
        "Treatment plan".to_owned(),
        "—".to_owned(),
        "Referred for".to_owned(),
        "—".to_owned(),
        "Referral notes".to_owned(),
        "—".to_owned(),
        "Remarks".to_owned(),
        "review in 2 weeks".to_owned(),
        format!("{display_id} · 1 / 1"),
    ]
    .join(" ");
    assert_eq!(pages[0], expected);
}

#[tokio::test]
async fn long_consult_pdf_continues_on_a_second_page_numbered_two_of_two() {
    let mut client = Client::signed_in(DOCTOR).await;
    let village = create_village(&mut client).await;
    let patient = register_patient(&mut client, id(&village)).await;
    let consultation = (1..=70).map(|n| format!("Observation number {n}.")).collect::<Vec<_>>().join("\n");
    let consult = client
        .post("/consults", json!({ "visitId": patient["visitId"], "consultation": consultation, "diagnoses": [{ "details": "long" }] }))
        .await
        .ok()
        .json();

    let pdf = client.get(&format!("/consults/{}/pdf", id(&consult))).await.expect(StatusCode::OK);

    assert_eq!(page_count(&pdf.bytes), 2);
    let Some(pages) = report_pages(&pdf.bytes) else { return };
    assert_eq!(pages.len(), 2);
    let display_id = patient["displayId"].as_str().unwrap();
    let header = format!("Consultation report {display_id} · {}", patient["name"].as_str().unwrap());
    assert!(pages[0].starts_with(&header), "{}", pages[0]);
    assert!(pages[1].starts_with(&header), "{}", pages[1]);
    assert!(pages[0].ends_with(&format!("{display_id} · 1 / 2")), "{}", pages[0]);
    assert!(pages[1].ends_with(&format!("Remarks — {display_id} · 2 / 2")), "{}", pages[1]);
    assert!(pages[0].contains("Observation number 1. Observation number 2."));
    assert!(pages[1].contains("Observation number 70. Diagnoses Uncategorised: long"));
    let both = pages.join(" ");
    assert!((1..=70).all(|n| both.matches(&format!("Observation number {n}.")).count() == 1), "every line is printed once");
}

#[tokio::test]
async fn consult_pdf_uses_the_visit_village_when_the_patient_has_none() {
    let mut client = Client::admin().await;
    let village = create_village(&mut client).await;
    let patient = register_patient(&mut client, id(&village)).await;
    let consult = create_consult(&mut client, patient["visitId"].as_i64().unwrap()).await;
    sqlx::query(r#"UPDATE "Patients" SET "VillageCodeId" = NULL WHERE "Id" = $1"#)
        .bind(id(&patient) as i32)
        .execute(&db().await)
        .await
        .unwrap();

    let pdf = client.get(&format!("/consults/{}/pdf", id(&consult))).await.expect(StatusCode::OK);

    let display_id = patient["displayId"].as_str().unwrap();
    let disposition = pdf.header("content-disposition").unwrap();
    assert!(disposition.contains(&format!("_report_{display_id}.pdf")), "{disposition}");
    let Some(pages) = report_pages(&pdf.bytes) else { return };
    assert!(pages[0].contains(&format!("{display_id} · {}", patient["name"].as_str().unwrap())), "{}", pages[0]);
}

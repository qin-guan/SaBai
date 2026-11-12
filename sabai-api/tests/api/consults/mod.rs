//! Consults, diagnoses, referrals, the patient chart views and the consult PDF.

mod chart;
mod consult;
mod consult_edge;
mod create;
mod diagnosis;
mod pdf;
mod referral;

use std::io::Write;
use std::process::{Command, Stdio};

use serde_json::{Value, json};

use crate::support::*;

/// A new village with one registered patient called `name`: `(patient id, visit id)`.
async fn register(client: &mut Client, name: &str) -> (i64, i64) {
    let village = create_village(client).await;
    let patient = register_patient_named(client, id(&village), name).await;
    (id(&patient), patient["visitId"].as_i64().unwrap())
}

/// Records a consult with one respiratory diagnosis reading `details`; returns its id.
async fn consult_with(client: &mut Client, visit_id: i64, details: &str) -> i64 {
    let consult = client
        .post(
            "/consults",
            json!({
                "visitId": visit_id,
                "consultation": "exam",
                "diagnoses": [{ "details": details, "category": "Respiratory" }],
            }),
        )
        .await
        .ok()
        .json();
    id(&consult)
}

/// A consult for a newly registered patient called `patient_name`: `(consult id, visit id)`.
async fn consult_for_new_patient(client: &mut Client, patient_name: &str) -> (i64, i64) {
    let (_, visit_id) = register(client, patient_name).await;
    let consult_id = consult_with(client, visit_id, &format!("consult {}", suffix())).await;
    (consult_id, visit_id)
}

/// A medication brand, inserted directly: the pharmacy endpoints are not part of this area.
async fn medication_brand(name: &str) -> i64 {
    let db = db().await;
    let ingredient: i32 = sqlx::query_scalar(
        r#"INSERT INTO "MedicationActiveIngredients" ("Name", "UnitOfMeasurement", "FallBelow")
           VALUES ($1, 'mg', 5) RETURNING "Id""#,
    )
    .bind(format!("{name} ingredient"))
    .fetch_one(&db)
    .await
    .unwrap();
    let brand: i32 = sqlx::query_scalar(
        r#"INSERT INTO "MedicationBrands" ("Name", "ActiveIngredientId", "Remarks") VALUES ($1, $2, '') RETURNING "Id""#,
    )
    .bind(name)
    .bind(ingredient)
    .fetch_one(&db)
    .await
    .unwrap();
    brand.into()
}

/// Another visit of the patient on the given date, inserted directly: opening visits is
/// not part of this area.
async fn visit_on(patient_id: i64, village_id: i64, date: &str) -> i64 {
    let visit: i32 = sqlx::query_scalar(
        r#"INSERT INTO "Visits" ("PatientId", "VillageCodeId", "Date", "Status")
           VALUES ($1, $2, $3::timestamptz, 'started') RETURNING "Id""#,
    )
    .bind(patient_id as i32)
    .bind(village_id as i32)
    .bind(date)
    .fetch_one(&db().await)
    .await
    .unwrap();
    visit.into()
}

/// Moves a consult's date into the past.
async fn backdate_consult(consult_id: i64, hours: i32) {
    sqlx::query(r#"UPDATE "Consults" SET "Date" = now() - make_interval(hours => $2) WHERE "Id" = $1"#)
        .bind(consult_id as i32)
        .bind(hours)
        .execute(&db().await)
        .await
        .unwrap();
}

/// The audit log entries of one record, newest first.
async fn audit_entries(admin: &mut Client, entity_type: &str, entity_id: i64) -> Vec<Value> {
    let log = admin.get(&format!("/audit-logs?entityType={entity_type}&entityId={entity_id}&take=50")).await.ok().json();
    log["entries"].as_array().unwrap().clone()
}

/// The changed fields of an audit entry, in order.
fn changed_fields(entry: &Value) -> Vec<&str> {
    entry["changes"].as_array().unwrap().iter().map(|change| change["field"].as_str().unwrap()).collect()
}

/// The text of each page of a PDF as `pdftotext` (poppler) reads it, or `None` on a
/// machine without it.
fn pdf_pages(pdf: &[u8]) -> Option<Vec<String>> {
    let mut child = Command::new("pdftotext")
        .args(["-layout", "-", "-"])
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::null())
        .spawn()
        .ok()?;
    let mut stdin = child.stdin.take()?;
    let input = pdf.to_vec();
    let writer = std::thread::spawn(move || stdin.write_all(&input));
    let output = child.wait_with_output().ok()?;
    writer.join().ok()?.ok()?;
    assert!(output.status.success(), "pdftotext could not read the PDF");
    let text = String::from_utf8_lossy(&output.stdout).into_owned();
    let mut pages: Vec<String> = text.split('\u{c}').map(str::to_owned).collect();
    if pages.last().is_some_and(|page| page.trim().is_empty()) {
        pages.pop();
    }
    Some(pages)
}

/// Runs of spaces collapsed, so text can be compared whatever the column layout was.
fn squeeze(text: &str) -> String {
    text.split_whitespace().collect::<Vec<_>>().join(" ")
}

/// The number of pages the PDF declares.
fn page_count(pdf: &[u8]) -> usize {
    let text = String::from_utf8_lossy(pdf);
    text.matches("/Type /Page\n").count() + text.matches("/Type /Page ").count() + text.matches("/Type /Page>").count()
}

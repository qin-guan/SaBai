//! Patients: registration, lookup and the patient record.

pub mod journey;
pub mod photo;

use std::collections::HashMap;

use axum::Router;
use axum::routing::get;
use serde::{Deserialize, Serialize};
use sqlx::PgConnection;

use crate::auth::Staff;
use crate::domain::{Decimal, Gender, Patient, VillageCode, Visit, Vitals};
use crate::error::{ApiError, ApiResult, OrNotFound};
use crate::http::{Created, Ctx, Json, NoContent, Req, Rules, Validate};
use crate::routes::visits::{SELECT_VISITS, VisitResponse};
use crate::state::AppState;
use crate::time::Ts;

pub use journey::VisitJourney;

const NOT_FOUND: &str = "Patient was not found.";

const FACE_PERSON_PREFIX: &str = "patient:";

/// The `personId` a patient's faces are enrolled under in the face store. Patients are
/// namespaced (`patient:42`) so they can never collide with identities enrolled through
/// the generic `/persons` endpoints.
pub fn face_person_id(patient_id: i32) -> String {
    format!("{FACE_PERSON_PREFIX}{patient_id}")
}

/// The patient a face store `personId` stands for, if it is a patient's.
pub fn patient_of_face_person(person_id: &str) -> Option<i32> {
    person_id.strip_prefix(FACE_PERSON_PREFIX)?.trim().parse().ok()
}

pub(crate) async fn find(conn: &mut PgConnection, id: i32) -> ApiResult<Patient> {
    sqlx::query_as(r#"SELECT * FROM "Patients" WHERE "Id" = $1"#).bind(id).fetch_optional(conn).await.or_not_found(NOT_FOUND)
}

/// Blank text is no value.
pub fn normalize(value: Option<&str>) -> Option<String> {
    value.map(str::trim).filter(|value| !value.is_empty()).map(str::to_owned)
}

/// Registration payload. A visit is opened for the patient in the same transaction.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateRequest {
    pub name: String,
    pub identification_number: Option<String>,
    pub contact_no: Option<String>,
    pub gender: Option<Gender>,
    pub date_of_birth: Option<Ts>,
    pub drug_allergy: String,
    #[serde(default)]
    pub has_poor_card: bool,
    #[serde(default, rename = "hasBS2Card")]
    pub has_bs2_card: bool,
    #[serde(default)]
    pub has_sabai_card: bool,
    #[serde(default)]
    pub to_get_report: bool,
    #[serde(default)]
    pub village_code_id: i32,
    pub vitals: Option<VitalsRequest>,
}

/// Optional vitals captured during registration.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct VitalsRequest {
    pub temperature: Option<Decimal>,
    pub height: Option<Decimal>,
    pub weight: Option<Decimal>,
    pub systolic: Option<i32>,
    pub diastolic: Option<i32>,
    pub heart_rate: Option<i32>,
    pub hemocue_count: Option<Decimal>,
    pub diabetes_mellitus: Option<bool>,
    pub urine_test: Option<String>,
    pub blood_glucose_non_fasting: Option<Decimal>,
    pub blood_glucose_fasting: Option<Decimal>,
    pub hba1c: Option<Decimal>,
    pub others: Option<String>,
}

impl Validate for CreateRequest {
    fn validate(&self, v: &mut Rules) {
        v.not_empty("name", &self.name).max_len("name", &self.name, 255);
        v.max_len("identificationNumber", &self.identification_number, 255);
        v.max_len("contactNo", &self.contact_no, 255);
        v.not_null("gender", &self.gender);
        v.not_null("dateOfBirth", &self.date_of_birth);
        v.not_empty("drugAllergy", &self.drug_allergy);
        v.greater_than("villageCodeId", self.village_code_id, 0);
    }
}

/// Newly registered patient, the visit opened for them, and vitals when captured.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateResponse {
    pub id: i32,
    pub name: String,
    pub identification_number: Option<String>,
    pub contact_no: Option<String>,
    pub gender: Gender,
    pub date_of_birth: Ts,
    pub drug_allergy: String,
    pub has_poor_card: bool,
    #[serde(rename = "hasBS2Card")]
    pub has_bs2_card: bool,
    pub has_sabai_card: bool,
    pub to_get_report: bool,
    pub village_code_id: Option<i32>,
    pub village_code: Option<String>,
    pub display_id: String,
    pub filter_string: String,
    pub last_visit_date: Ts,
    pub last_visit_id: i32,
    pub visit_id: i32,
    pub vitals_id: Option<i32>,
    pub has_photo: bool,
    pub has_face: bool,
}

/// Register a patient and open a visit.
async fn create(ctx: Ctx, _: Staff, Req(req): Req<CreateRequest>) -> ApiResult<Created<CreateResponse>> {
    let mut tx = ctx.begin().await?;
    let village: VillageCode = sqlx::query_as(r#"SELECT * FROM "VillageCodes" WHERE "Id" = $1"#)
        .bind(req.village_code_id)
        .fetch_optional(&mut *tx)
        .await
        .or_not_found("Village code was not found.")?;

    let now = Ts::now();
    let patient = tx
        .insert(Patient {
            id: 0,
            village_code_id: Some(village.id),
            name: req.name.trim().to_owned(),
            identification_number: normalize(req.identification_number.as_deref()),
            contact_no: normalize(req.contact_no.as_deref()),
            gender: req.gender.ok_or_else(|| ApiError::bad_request("Gender is required."))?,
            date_of_birth: req.date_of_birth.ok_or_else(|| ApiError::bad_request("Date of birth is required."))?,
            drug_allergy: req.drug_allergy.trim().to_owned(),
            has_poor_card: req.has_poor_card,
            has_bs2_card: req.has_bs2_card,
            has_sabai_card: req.has_sabai_card,
            to_get_report: req.to_get_report,
            photo_stored_file_name: None,
            latest_face_id: None,
            created_at: now,
        })
        .await?;
    let visit = tx
        .insert(Visit {
            id: 0,
            patient_id: patient.id,
            village_code_id: village.id,
            date: now,
            status: Visit::STARTED_STATUS.to_owned(),
        })
        .await?;
    let vitals = match &req.vitals {
        Some(vitals) => Some(
            tx.insert(Vitals {
                id: 0,
                visit_id: visit.id,
                temperature: vitals.temperature,
                height: vitals.height,
                weight: vitals.weight,
                systolic: vitals.systolic,
                diastolic: vitals.diastolic,
                heart_rate: vitals.heart_rate,
                hemocue_count: vitals.hemocue_count,
                diabetes_mellitus: vitals.diabetes_mellitus,
                urine_test: normalize(vitals.urine_test.as_deref()),
                blood_glucose_non_fasting: vitals.blood_glucose_non_fasting,
                blood_glucose_fasting: vitals.blood_glucose_fasting,
                hba1c: vitals.hba1c,
                others: normalize(vitals.others.as_deref()),
                recorded_at: None,
            })
            .await?,
        ),
        None => None,
    };
    tx.commit().await?;

    let response = CreateResponse {
        id: patient.id,
        display_id: Patient::format_display_id(Some(&village.code), patient.id),
        filter_string: Patient::format_filter_string(
            Some(&village.code),
            patient.id,
            patient.contact_no.as_deref(),
            &patient.name,
        ),
        name: patient.name,
        identification_number: patient.identification_number,
        contact_no: patient.contact_no,
        gender: patient.gender,
        date_of_birth: patient.date_of_birth,
        drug_allergy: patient.drug_allergy,
        has_poor_card: patient.has_poor_card,
        has_bs2_card: patient.has_bs2_card,
        has_sabai_card: patient.has_sabai_card,
        to_get_report: patient.to_get_report,
        village_code_id: patient.village_code_id,
        village_code: Some(village.code),
        last_visit_date: visit.date,
        last_visit_id: visit.id,
        visit_id: visit.id,
        vitals_id: vitals.map(|vitals| vitals.id),
        has_photo: false,
        has_face: false,
    };
    Ok(Created::at(format!("/patients/{}", response.id), response))
}

/// Identifies a patient.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct IdRequest {
    #[serde(default)]
    pub id: i32,
}

impl Validate for IdRequest {}

/// A patient with the code and colour of their village.
#[derive(Debug, sqlx::FromRow)]
struct PatientRow {
    #[sqlx(flatten)]
    patient: Patient,
    #[sqlx(rename = "VillageCode")]
    village_code: Option<String>,
    #[sqlx(rename = "VillageColorHex")]
    village_color_hex: Option<String>,
}

const SELECT_PATIENTS: &str = r#"
    SELECT p.*, vc."Code" AS "VillageCode", vc."ColorHex" AS "VillageColorHex"
    FROM "Patients" p
    LEFT JOIN "VillageCodes" vc ON vc."Id" = p."VillageCodeId"
"#;

#[derive(Debug, Clone, Copy, sqlx::FromRow)]
#[sqlx(rename_all = "PascalCase")]
struct LatestVisit {
    id: i32,
    patient_id: i32,
    date: Ts,
}

/// The latest visit of each of the patients, by patient id.
async fn latest_visits(conn: &mut PgConnection, patient_ids: &[i32]) -> Result<HashMap<i32, LatestVisit>, sqlx::Error> {
    if patient_ids.is_empty() {
        return Ok(HashMap::new());
    }
    let visits: Vec<LatestVisit> = sqlx::query_as(
        r#"SELECT DISTINCT ON ("PatientId") "Id", "PatientId", "Date"
           FROM "Visits"
           WHERE "PatientId" = ANY($1)
           ORDER BY "PatientId", "Date" DESC, "Id" DESC"#,
    )
    .bind(patient_ids)
    .fetch_all(conn)
    .await?;
    Ok(visits.into_iter().map(|visit| (visit.patient_id, visit)).collect())
}

/// A patient, including display id and latest visit.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PatientResponse {
    pub id: i32,
    pub name: String,
    pub identification_number: Option<String>,
    pub contact_no: Option<String>,
    pub gender: Gender,
    pub date_of_birth: Ts,
    pub drug_allergy: String,
    pub has_poor_card: bool,
    #[serde(rename = "hasBS2Card")]
    pub has_bs2_card: bool,
    pub has_sabai_card: bool,
    pub to_get_report: bool,
    pub village_code_id: Option<i32>,
    pub village_code: Option<String>,
    pub display_id: String,
    pub filter_string: String,
    pub last_visit_date: Option<Ts>,
    pub last_visit_id: Option<i32>,
    pub has_photo: bool,
    pub has_face: bool,
}

impl PatientResponse {
    fn new(row: PatientRow, last_visit: Option<LatestVisit>) -> Self {
        let PatientRow { patient, village_code, .. } = row;
        PatientResponse {
            id: patient.id,
            display_id: Patient::format_display_id(village_code.as_deref(), patient.id),
            filter_string: Patient::format_filter_string(
                village_code.as_deref(),
                patient.id,
                patient.contact_no.as_deref(),
                &patient.name,
            ),
            name: patient.name,
            identification_number: patient.identification_number,
            contact_no: patient.contact_no,
            gender: patient.gender,
            date_of_birth: patient.date_of_birth,
            drug_allergy: patient.drug_allergy,
            has_poor_card: patient.has_poor_card,
            has_bs2_card: patient.has_bs2_card,
            has_sabai_card: patient.has_sabai_card,
            to_get_report: patient.to_get_report,
            village_code_id: patient.village_code_id,
            village_code,
            last_visit_date: last_visit.map(|visit| visit.date),
            last_visit_id: last_visit.map(|visit| visit.id),
            has_photo: patient.photo_stored_file_name.is_some(),
            has_face: patient.latest_face_id.is_some(),
        }
    }
}

async fn load(conn: &mut PgConnection, id: i32) -> ApiResult<PatientResponse> {
    let row: PatientRow = sqlx::query_as(&format!(r#"{SELECT_PATIENTS} WHERE p."Id" = $1"#))
        .bind(id)
        .fetch_optional(&mut *conn)
        .await
        .or_not_found(NOT_FOUND)?;
    let last_visit = latest_visits(conn, &[id]).await?.remove(&id);
    Ok(PatientResponse::new(row, last_visit))
}

/// Get a patient.
async fn get_one(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<Json<PatientResponse>> {
    let mut conn = ctx.db().acquire().await?;
    Ok(Json(load(&mut conn, req.id).await?))
}

/// Optional name and village-code filters for the patient list.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ListRequest {
    pub name: Option<String>,
    pub village_code: Option<String>,
}

impl Validate for ListRequest {}

/// Patients ordered by id.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ListResponse {
    pub patients: Vec<PatientResponse>,
}

/// List patients.
async fn list(ctx: Ctx, _: Staff, Req(req): Req<ListRequest>) -> ApiResult<Json<ListResponse>> {
    let name = normalize(req.name.as_deref()).map(|name| name.to_lowercase());
    let village_code = normalize(req.village_code.as_deref()).map(|code| code.to_lowercase());

    let mut conn = ctx.db().acquire().await?;
    let rows: Vec<PatientRow> = sqlx::query_as(&format!(
        r#"{SELECT_PATIENTS}
           WHERE ($1::text IS NULL OR strpos(lower(p."Name"), $1) > 0)
             AND ($2::text IS NULL OR (vc."Id" IS NOT NULL AND lower(vc."Code") = $2))
           ORDER BY p."Id""#
    ))
    .bind(name)
    .bind(village_code)
    .fetch_all(&mut *conn)
    .await?;
    let ids: Vec<i32> = rows.iter().map(|row| row.patient.id).collect();
    let latest = latest_visits(&mut conn, &ids).await?;

    let patients = rows
        .into_iter()
        .map(|row| {
            let last_visit = latest.get(&row.patient.id).copied();
            PatientResponse::new(row, last_visit)
        })
        .collect();
    Ok(Json(ListResponse { patients }))
}

/// Patients with a visit first, then by latest visit date descending.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct WithLatestVisitResponse {
    pub patients: Vec<WithLatestVisitItem>,
}

/// A patient row including village color, latest visit and where that visit is in the clinic flow.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct WithLatestVisitItem {
    pub id: i32,
    pub name: String,
    pub identification_number: Option<String>,
    pub contact_no: Option<String>,
    pub gender: Gender,
    pub date_of_birth: Ts,
    pub drug_allergy: String,
    pub has_poor_card: bool,
    #[serde(rename = "hasBS2Card")]
    pub has_bs2_card: bool,
    pub has_sabai_card: bool,
    pub to_get_report: bool,
    pub village_code_id: Option<i32>,
    pub village_code: Option<String>,
    pub village_color_hex: Option<String>,
    pub display_id: String,
    pub filter_string: String,
    pub last_visit_date: Option<Ts>,
    pub last_visit_id: Option<i32>,
    pub has_photo: bool,
    pub has_face: bool,
    pub last_visit_journey: Option<VisitJourney>,
}

/// List patients with their latest visit.
async fn with_latest_visit(ctx: Ctx, _: Staff) -> ApiResult<Json<WithLatestVisitResponse>> {
    let mut conn = ctx.db().acquire().await?;
    let rows: Vec<PatientRow> = sqlx::query_as(SELECT_PATIENTS).fetch_all(&mut *conn).await?;
    let ids: Vec<i32> = rows.iter().map(|row| row.patient.id).collect();
    let latest = latest_visits(&mut conn, &ids).await?;
    let visit_ids: Vec<i32> = latest.values().map(|visit| visit.id).collect();
    let mut journeys = VisitJourney::for_visits(&mut conn, &visit_ids).await?;

    let mut rows: Vec<(PatientRow, Option<LatestVisit>)> = rows
        .into_iter()
        .map(|row| {
            let last_visit = latest.get(&row.patient.id).copied();
            (row, last_visit)
        })
        .collect();
    rows.sort_by(|(a, a_last), (b, b_last)| {
        b_last
            .is_some()
            .cmp(&a_last.is_some())
            .then_with(|| b_last.map(|visit| visit.date).cmp(&a_last.map(|visit| visit.date)))
            .then_with(|| a.patient.id.cmp(&b.patient.id))
    });

    let patients = rows
        .into_iter()
        .map(|(row, last_visit)| {
            let village_color_hex = row.village_color_hex.clone();
            let last_visit_journey = last_visit.and_then(|visit| journeys.remove(&visit.id));
            let patient = PatientResponse::new(row, last_visit);
            WithLatestVisitItem {
                id: patient.id,
                name: patient.name,
                identification_number: patient.identification_number,
                contact_no: patient.contact_no,
                gender: patient.gender,
                date_of_birth: patient.date_of_birth,
                drug_allergy: patient.drug_allergy,
                has_poor_card: patient.has_poor_card,
                has_bs2_card: patient.has_bs2_card,
                has_sabai_card: patient.has_sabai_card,
                to_get_report: patient.to_get_report,
                village_code_id: patient.village_code_id,
                village_code: patient.village_code,
                village_color_hex,
                display_id: patient.display_id,
                filter_string: patient.filter_string,
                last_visit_date: patient.last_visit_date,
                last_visit_id: patient.last_visit_id,
                has_photo: patient.has_photo,
                has_face: patient.has_face,
                last_visit_journey,
            }
        })
        .collect();
    Ok(Json(WithLatestVisitResponse { patients }))
}

/// Partial patient update. Image fields are not accepted.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateRequest {
    #[serde(default)]
    pub id: i32,
    pub name: Option<String>,
    pub identification_number: Option<String>,
    pub contact_no: Option<String>,
    pub gender: Option<Gender>,
    pub date_of_birth: Option<Ts>,
    pub drug_allergy: Option<String>,
    pub has_poor_card: Option<bool>,
    #[serde(rename = "hasBS2Card")]
    pub has_bs2_card: Option<bool>,
    pub has_sabai_card: Option<bool>,
    pub to_get_report: Option<bool>,
    pub village_code_id: Option<i32>,
}

impl Validate for UpdateRequest {
    fn validate(&self, v: &mut Rules) {
        if self.name.is_some() {
            v.not_empty("name", &self.name).max_len("name", &self.name, 255);
        }
        v.max_len("identificationNumber", &self.identification_number, 255);
        v.max_len("contactNo", &self.contact_no, 255);
        if self.drug_allergy.is_some() {
            v.not_empty("drugAllergy", &self.drug_allergy);
        }
        v.greater_than("villageCodeId", self.village_code_id, 0);
    }
}

/// Update a patient.
async fn update(ctx: Ctx, _: Staff, Req(req): Req<UpdateRequest>) -> ApiResult<Json<PatientResponse>> {
    let mut tx = ctx.begin().await?;
    let before = find(&mut tx, req.id).await?;
    let mut patient = before.clone();

    if let Some(name) = &req.name {
        patient.name = name.trim().to_owned();
    }
    if let Some(identification_number) = &req.identification_number {
        let identification_number = identification_number.trim();
        if !identification_number.is_empty() {
            let taken: bool = sqlx::query_scalar(
                r#"SELECT EXISTS (SELECT 1 FROM "Patients" WHERE "Id" <> $1 AND "IdentificationNumber" = $2)"#,
            )
            .bind(patient.id)
            .bind(identification_number)
            .fetch_one(&mut *tx)
            .await?;
            if taken {
                return Err(ApiError::conflict("Another patient already has this identification number."));
            }
        }
        patient.identification_number = normalize(Some(identification_number));
    }
    if let Some(contact_no) = &req.contact_no {
        patient.contact_no = normalize(Some(contact_no));
    }
    if let Some(gender) = req.gender {
        patient.gender = gender;
    }
    if let Some(date_of_birth) = req.date_of_birth {
        patient.date_of_birth = date_of_birth;
    }
    if let Some(drug_allergy) = &req.drug_allergy {
        patient.drug_allergy = drug_allergy.trim().to_owned();
    }
    if let Some(has_poor_card) = req.has_poor_card {
        patient.has_poor_card = has_poor_card;
    }
    if let Some(has_bs2_card) = req.has_bs2_card {
        patient.has_bs2_card = has_bs2_card;
    }
    if let Some(has_sabai_card) = req.has_sabai_card {
        patient.has_sabai_card = has_sabai_card;
    }
    if let Some(to_get_report) = req.to_get_report {
        patient.to_get_report = to_get_report;
    }
    if let Some(village_code_id) = req.village_code_id {
        let exists: bool = sqlx::query_scalar(r#"SELECT EXISTS (SELECT 1 FROM "VillageCodes" WHERE "Id" = $1)"#)
            .bind(village_code_id)
            .fetch_one(&mut *tx)
            .await?;
        if !exists {
            return Err(ApiError::not_found("Village code was not found."));
        }
        patient.village_code_id = Some(village_code_id);
    }

    let patient = tx.update(&before, patient).await?;
    let response = load(&mut tx, patient.id).await?;
    tx.commit().await?;
    Ok(Json(response))
}

/// Delete a patient.
async fn delete(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<NoContent> {
    let mut tx = ctx.begin().await?;
    let patient = find(&mut tx, req.id).await?;
    let has_visits: bool = sqlx::query_scalar(r#"SELECT EXISTS (SELECT 1 FROM "Visits" WHERE "PatientId" = $1)"#)
        .bind(patient.id)
        .fetch_one(&mut *tx)
        .await?;
    if has_visits {
        return Err(ApiError::conflict("Patient has visits and cannot be deleted."));
    }

    photo::delete_object(&ctx, patient.photo_stored_file_name.as_deref()).await;
    if patient.latest_face_id.is_some() {
        ctx.state.faces.delete_person(&face_person_id(patient.id)).await?;
    }

    tx.delete(&patient).await?;
    tx.commit().await?;
    Ok(NoContent)
}

/// Identifies the patient whose visits are listed.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct VisitsRequest {
    #[serde(default)]
    pub patient_id: i32,
}

impl Validate for VisitsRequest {}

/// Visits for one patient, newest first.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct VisitsResponse {
    pub visits: Vec<VisitWithJourney>,
}

/// A visit joined to its patient and village, with where it is in the clinic flow.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct VisitWithJourney {
    #[serde(flatten)]
    pub visit: VisitResponse,
    pub journey: VisitJourney,
}

/// List visits for a patient.
async fn visits(ctx: Ctx, _: Staff, Req(req): Req<VisitsRequest>) -> ApiResult<Json<VisitsResponse>> {
    let mut conn = ctx.db().acquire().await?;
    find(&mut conn, req.patient_id).await?;

    let visits: Vec<VisitResponse> =
        sqlx::query_as(&format!(r#"{SELECT_VISITS} WHERE v."PatientId" = $1 ORDER BY v."Date" DESC, v."Id" DESC"#))
            .bind(req.patient_id)
            .fetch_all(&mut *conn)
            .await?;
    let ids: Vec<i32> = visits.iter().map(|visit| visit.id).collect();
    let mut journeys = VisitJourney::for_visits(&mut conn, &ids).await?;

    let visits = visits
        .into_iter()
        .map(|visit| {
            let journey = journeys.remove(&visit.id).unwrap_or_default();
            VisitWithJourney { visit, journey }
        })
        .collect();
    Ok(Json(VisitsResponse { visits }))
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/patients", get(list).post(create))
        .route("/patients/with-latest-visit", get(with_latest_visit))
        .route("/patients/{id}", get(get_one).patch(update).delete(delete))
        .route("/patients/{patientId}/visits", get(visits))
        .merge(photo::router())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn face_person_ids_round_trip() {
        assert_eq!(face_person_id(42), "patient:42");
        assert_eq!(patient_of_face_person("patient:42"), Some(42));
        assert_eq!(patient_of_face_person("Patient:42"), None);
        assert_eq!(patient_of_face_person("patient:x"), None);
        assert_eq!(patient_of_face_person("someone"), None);
    }
}

//! Patients: registration, lookup and the patient record.

use axum::Router;
use axum::routing::post;
use serde::{Deserialize, Serialize};

use crate::auth::Staff;
use crate::domain::{Decimal, Gender, Patient, VillageCode, Visit, Vitals};
use crate::error::{ApiError, ApiResult, OrNotFound};
use crate::http::{Created, Ctx, Req, Rules, Validate};
use crate::state::AppState;
use crate::time::Ts;

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

pub fn router() -> Router<AppState> {
    Router::new().route("/patients", post(create))
}

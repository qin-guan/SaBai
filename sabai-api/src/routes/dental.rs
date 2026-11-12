//! The dental station's consultation for a visit. Saved as a whole: one per visit.

use std::fmt;
use std::marker::PhantomData;

use axum::Router;
use axum::routing::get;
use chrono::NaiveDate;
use serde::de::value::{StrDeserializer, U64Deserializer};
use serde::de::{self, Deserializer, Visitor};
use serde::{Deserialize, Serialize};

use super::vitals::{VisitRequest, conflict_if_duplicate, free_text};
use crate::auth::Staff;
use crate::domain::{
    AbsentPresent, BrushingFrequency, CariesStatus, DentalConsult, DentalReferralPriority, DentalReferralType,
    FacialSymmetry, GingivalCondition, Lymphadenopathy, Malocclusion, MouthOpening, OralHygiene, PlaqueLevel,
    StoredFile, SugarConsumption, Visit, YesNoUnknown,
};
use crate::error::{ApiError, ApiResult, OrNotFound};
use crate::http::validate::display_name;
use crate::http::{Ctx, Json, Req, Rules, Validate};
use crate::state::AppState;
use crate::time::Ts;

/// A visit's dental consultation.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct DentalConsultResponse {
    pub id: i32,
    pub visit_id: i32,
    pub previous_dental_visit: Option<YesNoUnknown>,
    pub previous_dental_treatment: Option<YesNoUnknown>,
    pub toothache: Option<bool>,
    pub extraction: Option<bool>,
    pub dental_trauma: Option<bool>,
    pub current_toothache: Option<bool>,
    pub tooth_sensitivity: Option<bool>,
    pub bleeding_gums: Option<bool>,
    pub brushing_frequency: Option<BrushingFrequency>,
    pub fluoride_toothpaste: Option<YesNoUnknown>,
    pub sugar_consumption: Option<SugarConsumption>,
    pub other_dental_history: Option<String>,
    pub chief_complaint: Option<String>,
    pub facial_symmetry: Option<FacialSymmetry>,
    pub facial_swelling: Option<AbsentPresent>,
    pub lymphadenopathy: Option<Lymphadenopathy>,
    pub mouth_opening: Option<MouthOpening>,
    pub oral_hygiene: Option<OralHygiene>,
    pub plaque: Option<PlaqueLevel>,
    pub calculus: Option<AbsentPresent>,
    pub gingival_condition: Option<GingivalCondition>,
    pub caries: Option<CariesStatus>,
    pub missing_teeth: Option<AbsentPresent>,
    pub fractured_teeth: Option<AbsentPresent>,
    pub mucosal_lesions: Option<AbsentPresent>,
    pub malocclusion: Option<Malocclusion>,
    pub diagnoses: Vec<String>,
    pub fluoride_done: Option<bool>,
    pub fluoride_date: Option<NaiveDate>,
    pub fluoride_operator: Option<String>,
    pub fluoride_tooth_numbers: Option<String>,
    pub sealant_done: Option<bool>,
    pub sealant_date: Option<NaiveDate>,
    pub sealant_operator: Option<String>,
    pub sealant_tooth_numbers: Option<String>,
    pub sdf_done: Option<bool>,
    pub sdf_date: Option<NaiveDate>,
    pub sdf_operator: Option<String>,
    pub sdf_tooth_numbers: Option<String>,
    pub scaling_done: Option<bool>,
    pub scaling_date: Option<NaiveDate>,
    pub scaling_operator: Option<String>,
    pub scaling_tooth_numbers: Option<String>,
    pub referred_for: Option<DentalReferralType>,
    pub referral_priority: Option<DentalReferralPriority>,
    pub referral_notes: Option<String>,
    pub recommended_facility: Option<String>,
    pub remarks: Option<String>,
    /// Id of the consultation's image; fetch its bytes from `GET /files/{id}/content`.
    pub image_file_id: Option<i32>,
    pub updated_at: Ts,
}

impl From<DentalConsult> for DentalConsultResponse {
    fn from(d: DentalConsult) -> Self {
        DentalConsultResponse {
            id: d.id,
            visit_id: d.visit_id,
            previous_dental_visit: d.previous_dental_visit,
            previous_dental_treatment: d.previous_dental_treatment,
            toothache: d.toothache,
            extraction: d.extraction,
            dental_trauma: d.dental_trauma,
            current_toothache: d.current_toothache,
            tooth_sensitivity: d.tooth_sensitivity,
            bleeding_gums: d.bleeding_gums,
            brushing_frequency: d.brushing_frequency,
            fluoride_toothpaste: d.fluoride_toothpaste,
            sugar_consumption: d.sugar_consumption,
            other_dental_history: d.other_dental_history,
            chief_complaint: d.chief_complaint,
            facial_symmetry: d.facial_symmetry,
            facial_swelling: d.facial_swelling,
            lymphadenopathy: d.lymphadenopathy,
            mouth_opening: d.mouth_opening,
            oral_hygiene: d.oral_hygiene,
            plaque: d.plaque,
            calculus: d.calculus,
            gingival_condition: d.gingival_condition,
            caries: d.caries,
            missing_teeth: d.missing_teeth,
            fractured_teeth: d.fractured_teeth,
            mucosal_lesions: d.mucosal_lesions,
            malocclusion: d.malocclusion,
            diagnoses: d.diagnoses,
            fluoride_done: d.fluoride_done,
            fluoride_date: d.fluoride_date,
            fluoride_operator: d.fluoride_operator,
            fluoride_tooth_numbers: d.fluoride_tooth_numbers,
            sealant_done: d.sealant_done,
            sealant_date: d.sealant_date,
            sealant_operator: d.sealant_operator,
            sealant_tooth_numbers: d.sealant_tooth_numbers,
            sdf_done: d.sdf_done,
            sdf_date: d.sdf_date,
            sdf_operator: d.sdf_operator,
            sdf_tooth_numbers: d.sdf_tooth_numbers,
            scaling_done: d.scaling_done,
            scaling_date: d.scaling_date,
            scaling_operator: d.scaling_operator,
            scaling_tooth_numbers: d.scaling_tooth_numbers,
            referred_for: d.referred_for,
            referral_priority: d.referral_priority,
            referral_notes: d.referral_notes,
            recommended_facility: d.recommended_facility,
            remarks: d.remarks,
            image_file_id: d.image_file_id,
            updated_at: d.updated_at,
        }
    }
}

/// Get the dental consultation for a visit.
async fn get_by_visit(ctx: Ctx, _: Staff, Req(req): Req<VisitRequest>) -> ApiResult<Json<DentalConsultResponse>> {
    let consult: DentalConsult = sqlx::query_as(r#"SELECT * FROM "DentalConsults" WHERE "VisitId" = $1 LIMIT 1"#)
        .bind(req.visit_id)
        .fetch_optional(ctx.db())
        .await
        .or_not_found("Dental consultation was not found.")?;
    Ok(Json(consult.into()))
}

/// One of the form's options as it arrived. Options may be sent by name or by number;
/// a number that names no option is kept so validation can report it against the field.
#[derive(Debug, Clone, Copy)]
pub enum Choice<T> {
    Known(T),
    OutOfRange(i64),
}

impl<'de, T: Deserialize<'de>> Deserialize<'de> for Choice<T> {
    fn deserialize<D: Deserializer<'de>>(deserializer: D) -> Result<Self, D::Error> {
        struct ChoiceVisitor<T>(PhantomData<T>);

        impl<'de, T: Deserialize<'de>> Visitor<'de> for ChoiceVisitor<T> {
            type Value = Choice<T>;

            fn expecting(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
                f.write_str("an option's name or number")
            }

            fn visit_i64<E: de::Error>(self, number: i64) -> Result<Choice<T>, E> {
                let known =
                    u64::try_from(number).ok().and_then(|index| T::deserialize(U64Deserializer::<E>::new(index)).ok());
                Ok(known.map_or(Choice::OutOfRange(number), Choice::Known))
            }

            fn visit_u64<E: de::Error>(self, number: u64) -> Result<Choice<T>, E> {
                self.visit_i64(i64::try_from(number).unwrap_or(i64::MAX))
            }

            fn visit_str<E: de::Error>(self, text: &str) -> Result<Choice<T>, E> {
                match text.parse::<i64>() {
                    Ok(number) => self.visit_i64(number),
                    Err(_) => T::deserialize(StrDeserializer::<E>::new(text)).map(Choice::Known),
                }
            }
        }

        deserializer.deserialize_any(ChoiceVisitor(PhantomData))
    }
}

fn known<T: Copy>(choice: Option<Choice<T>>) -> Option<T> {
    match choice {
        Some(Choice::Known(value)) => Some(value),
        _ => None,
    }
}

/// The full dental consultation for a visit. It replaces whatever was saved before, so an
/// omitted field is cleared.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpsertRequest {
    #[serde(default)]
    pub visit_id: i32,
    pub previous_dental_visit: Option<Choice<YesNoUnknown>>,
    pub previous_dental_treatment: Option<Choice<YesNoUnknown>>,
    pub toothache: Option<bool>,
    pub extraction: Option<bool>,
    pub dental_trauma: Option<bool>,
    pub current_toothache: Option<bool>,
    pub tooth_sensitivity: Option<bool>,
    pub bleeding_gums: Option<bool>,
    pub brushing_frequency: Option<Choice<BrushingFrequency>>,
    pub fluoride_toothpaste: Option<Choice<YesNoUnknown>>,
    pub sugar_consumption: Option<Choice<SugarConsumption>>,
    pub other_dental_history: Option<String>,
    pub chief_complaint: Option<String>,
    pub facial_symmetry: Option<Choice<FacialSymmetry>>,
    pub facial_swelling: Option<Choice<AbsentPresent>>,
    pub lymphadenopathy: Option<Choice<Lymphadenopathy>>,
    pub mouth_opening: Option<Choice<MouthOpening>>,
    pub oral_hygiene: Option<Choice<OralHygiene>>,
    pub plaque: Option<Choice<PlaqueLevel>>,
    pub calculus: Option<Choice<AbsentPresent>>,
    pub gingival_condition: Option<Choice<GingivalCondition>>,
    pub caries: Option<Choice<CariesStatus>>,
    pub missing_teeth: Option<Choice<AbsentPresent>>,
    pub fractured_teeth: Option<Choice<AbsentPresent>>,
    pub mucosal_lesions: Option<Choice<AbsentPresent>>,
    pub malocclusion: Option<Choice<Malocclusion>>,
    pub diagnoses: Option<Vec<Option<String>>>,
    pub fluoride_done: Option<bool>,
    pub fluoride_date: Option<NaiveDate>,
    pub fluoride_operator: Option<String>,
    pub fluoride_tooth_numbers: Option<String>,
    pub sealant_done: Option<bool>,
    pub sealant_date: Option<NaiveDate>,
    pub sealant_operator: Option<String>,
    pub sealant_tooth_numbers: Option<String>,
    pub sdf_done: Option<bool>,
    pub sdf_date: Option<NaiveDate>,
    pub sdf_operator: Option<String>,
    pub sdf_tooth_numbers: Option<String>,
    pub scaling_done: Option<bool>,
    pub scaling_date: Option<NaiveDate>,
    pub scaling_operator: Option<String>,
    pub scaling_tooth_numbers: Option<String>,
    pub referred_for: Option<Choice<DentalReferralType>>,
    pub referral_priority: Option<Choice<DentalReferralPriority>>,
    pub referral_notes: Option<String>,
    pub recommended_facility: Option<String>,
    pub remarks: Option<String>,
    /// Id of a file uploaded through `POST /files` for this patient. Null clears it.
    pub image_file_id: Option<i32>,
}

impl Validate for UpsertRequest {
    fn validate(&self, v: &mut Rules) {
        fn in_range<T>(v: &mut Rules, name: &str, choice: &Option<Choice<T>>) {
            if let Some(Choice::OutOfRange(number)) = choice {
                v.add(
                    name,
                    format!("'{}' has a range of values which does not include '{number}'.", display_name(name)),
                );
            }
        }

        v.greater_than("visitId", self.visit_id, 0);
        in_range(v, "previousDentalVisit", &self.previous_dental_visit);
        in_range(v, "previousDentalTreatment", &self.previous_dental_treatment);
        in_range(v, "brushingFrequency", &self.brushing_frequency);
        in_range(v, "fluorideToothpaste", &self.fluoride_toothpaste);
        in_range(v, "sugarConsumption", &self.sugar_consumption);
        in_range(v, "facialSymmetry", &self.facial_symmetry);
        in_range(v, "facialSwelling", &self.facial_swelling);
        in_range(v, "lymphadenopathy", &self.lymphadenopathy);
        in_range(v, "mouthOpening", &self.mouth_opening);
        in_range(v, "oralHygiene", &self.oral_hygiene);
        in_range(v, "plaque", &self.plaque);
        in_range(v, "calculus", &self.calculus);
        in_range(v, "gingivalCondition", &self.gingival_condition);
        in_range(v, "caries", &self.caries);
        in_range(v, "missingTeeth", &self.missing_teeth);
        in_range(v, "fracturedTeeth", &self.fractured_teeth);
        in_range(v, "mucosalLesions", &self.mucosal_lesions);
        in_range(v, "malocclusion", &self.malocclusion);
        in_range(v, "referredFor", &self.referred_for);
        in_range(v, "referralPriority", &self.referral_priority);
        v.max_len("otherDentalHistory", &self.other_dental_history, 2000);
        v.max_len("chiefComplaint", &self.chief_complaint, 2000);
        v.max_len("fluorideOperator", &self.fluoride_operator, 200);
        v.max_len("fluorideToothNumbers", &self.fluoride_tooth_numbers, 200);
        v.max_len("sealantOperator", &self.sealant_operator, 200);
        v.max_len("sealantToothNumbers", &self.sealant_tooth_numbers, 200);
        v.max_len("sdfOperator", &self.sdf_operator, 200);
        v.max_len("sdfToothNumbers", &self.sdf_tooth_numbers, 200);
        v.max_len("scalingOperator", &self.scaling_operator, 200);
        v.max_len("scalingToothNumbers", &self.scaling_tooth_numbers, 200);
        v.max_len("referralNotes", &self.referral_notes, 2000);
        v.max_len("recommendedFacility", &self.recommended_facility, 200);
        v.max_len("remarks", &self.remarks, 2000);
        v.greater_than("imageFileId", self.image_file_id, 0);

        let diagnoses = self.diagnoses.as_deref().unwrap_or_default();
        v.must("diagnoses", diagnoses.len() <= 50, "At most 50 diagnoses can be recorded.");
        for (index, diagnosis) in diagnoses.iter().enumerate() {
            let name = format!("diagnoses[{index}]");
            match diagnosis {
                None => {
                    v.add(&name, "'diagnoses' must not be empty.");
                }
                Some(text) => {
                    let length = text.encode_utf16().count();
                    v.must(
                        &name,
                        length <= 500,
                        format!(
                            "The length of 'diagnoses' must be 500 characters or fewer. You entered {length} characters."
                        ),
                    );
                }
            }
        }
    }
}

impl UpsertRequest {
    /// The consultation this request describes. Everything the form holds comes from the
    /// request (blank text is no value, blank diagnoses are dropped); the rest is given.
    fn consult(&self, id: i32, updated_at: Ts, recorded_at: Option<Ts>) -> DentalConsult {
        let text = |value: &Option<String>| free_text(value.as_deref());
        DentalConsult {
            id,
            visit_id: self.visit_id,
            previous_dental_visit: known(self.previous_dental_visit),
            previous_dental_treatment: known(self.previous_dental_treatment),
            toothache: self.toothache,
            extraction: self.extraction,
            dental_trauma: self.dental_trauma,
            current_toothache: self.current_toothache,
            tooth_sensitivity: self.tooth_sensitivity,
            bleeding_gums: self.bleeding_gums,
            brushing_frequency: known(self.brushing_frequency),
            fluoride_toothpaste: known(self.fluoride_toothpaste),
            sugar_consumption: known(self.sugar_consumption),
            other_dental_history: text(&self.other_dental_history),
            chief_complaint: text(&self.chief_complaint),
            facial_symmetry: known(self.facial_symmetry),
            facial_swelling: known(self.facial_swelling),
            lymphadenopathy: known(self.lymphadenopathy),
            mouth_opening: known(self.mouth_opening),
            oral_hygiene: known(self.oral_hygiene),
            plaque: known(self.plaque),
            calculus: known(self.calculus),
            gingival_condition: known(self.gingival_condition),
            caries: known(self.caries),
            missing_teeth: known(self.missing_teeth),
            fractured_teeth: known(self.fractured_teeth),
            mucosal_lesions: known(self.mucosal_lesions),
            malocclusion: known(self.malocclusion),
            diagnoses: self.diagnoses.iter().flatten().filter_map(text).collect(),
            fluoride_done: self.fluoride_done,
            fluoride_date: self.fluoride_date,
            fluoride_operator: text(&self.fluoride_operator),
            fluoride_tooth_numbers: text(&self.fluoride_tooth_numbers),
            sealant_done: self.sealant_done,
            sealant_date: self.sealant_date,
            sealant_operator: text(&self.sealant_operator),
            sealant_tooth_numbers: text(&self.sealant_tooth_numbers),
            sdf_done: self.sdf_done,
            sdf_date: self.sdf_date,
            sdf_operator: text(&self.sdf_operator),
            sdf_tooth_numbers: text(&self.sdf_tooth_numbers),
            scaling_done: self.scaling_done,
            scaling_date: self.scaling_date,
            scaling_operator: text(&self.scaling_operator),
            scaling_tooth_numbers: text(&self.scaling_tooth_numbers),
            referred_for: known(self.referred_for),
            referral_priority: known(self.referral_priority),
            referral_notes: text(&self.referral_notes),
            recommended_facility: text(&self.recommended_facility),
            remarks: text(&self.remarks),
            image_file_id: self.image_file_id,
            updated_at,
            recorded_at,
        }
    }
}

/// Save the dental consultation for a visit, creating it if needed.
async fn upsert_by_visit(ctx: Ctx, _: Staff, Req(req): Req<UpsertRequest>) -> ApiResult<Json<DentalConsultResponse>> {
    let mut tx = ctx.begin().await?;
    let visit: Visit = sqlx::query_as(r#"SELECT * FROM "Visits" WHERE "Id" = $1"#)
        .bind(req.visit_id)
        .fetch_optional(&mut *tx)
        .await
        .or_not_found("Visit was not found.")?;

    if let Some(image_file_id) = req.image_file_id {
        let file: Option<StoredFile> = sqlx::query_as(r#"SELECT * FROM "Files" WHERE "Id" = $1 AND NOT "IsDeleted""#)
            .bind(image_file_id)
            .fetch_optional(&mut *tx)
            .await?;
        let file = file.ok_or_else(|| ApiError::bad_request("Image file was not found."))?;
        if file.patient_id.is_some_and(|patient_id| patient_id != visit.patient_id) {
            return Err(ApiError::bad_request("Image file belongs to a different patient."));
        }
    }

    let existing: Option<DentalConsult> =
        sqlx::query_as(r#"SELECT * FROM "DentalConsults" WHERE "VisitId" = $1 LIMIT 1"#)
            .bind(req.visit_id)
            .fetch_optional(&mut *tx)
            .await?;
    let now = Ts::now();
    let consult = match existing {
        Some(before) => {
            let after = req.consult(before.id, now, before.recorded_at);
            tx.update(&before, after).await?
        }
        None => tx.insert(req.consult(0, now, None)).await.map_err(|error| {
            conflict_if_duplicate(
                error,
                "The dental consultation for this visit was saved by someone else. Reload and try again.",
            )
        })?,
    };
    tx.commit().await?;
    Ok(Json(consult.into()))
}

pub fn router() -> Router<AppState> {
    Router::new().route("/visits/{visitId}/dental", get(get_by_visit).put(upsert_by_visit))
}

//! Consults: what the doctor found and decided for a visit.

use axum::Router;
use axum::routing::post;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use crate::auth::Staff;
use crate::domain::{Consult, Diagnosis, DiagnosisCategory, Order, OrderStatus};
use crate::error::{ApiError, ApiResult};
use crate::http::{Created, Ctx, Req, Rules, Validate};
use crate::state::AppState;
use crate::time::Ts;

/// New consult, its diagnoses, and any prescriptions to record together.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateRequest {
    #[serde(default)]
    pub visit_id: i32,
    pub past_medical_history: Option<String>,
    pub consultation: Option<String>,
    pub treatment_plan: Option<String>,
    pub referred_for: Option<String>,
    pub referral_notes: Option<String>,
    pub remarks: Option<String>,
    #[serde(default)]
    pub diagnoses: Option<Vec<DiagnosisInput>>,
    #[serde(default)]
    pub orders: Option<Vec<OrderInput>>,
}

/// Diagnosis captured with a new consult.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DiagnosisInput {
    #[serde(default)]
    pub details: String,
    pub category: Option<DiagnosisCategory>,
}

/// Prescription inserted with a new consult. Status is always pending.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct OrderInput {
    #[serde(default)]
    pub medication_brand_id: i32,
    #[serde(default)]
    pub quantity: i32,
    pub dosage_instructions: Option<String>,
    pub remarks: Option<String>,
}

impl Validate for CreateRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("visitId", self.visit_id, 0);
        let diagnoses = self.diagnoses.as_deref().unwrap_or_default();
        v.not_empty_list("diagnoses", diagnoses);
        for (index, diagnosis) in diagnoses.iter().enumerate() {
            v.not_empty(&format!("diagnoses[{index}].details"), &diagnosis.details);
        }
        for (index, order) in self.orders.iter().flatten().enumerate() {
            v.greater_than(&format!("orders[{index}].medicationBrandId"), order.medication_brand_id, 0);
            v.greater_than(&format!("orders[{index}].quantity"), order.quantity, 0);
        }
    }
}

/// Consult recorded by the current doctor, including its diagnoses.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ConsultResponse {
    pub id: i32,
    pub visit_id: i32,
    pub date: Ts,
    pub past_medical_history: Option<String>,
    pub consultation: Option<String>,
    pub treatment_plan: Option<String>,
    pub referred_for: Option<String>,
    pub referral_notes: Option<String>,
    pub remarks: Option<String>,
    pub doctor_id: Uuid,
    pub doctor_nickname: String,
    pub diagnoses: Vec<DiagnosisItem>,
}

/// Diagnosis recorded on a consult.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct DiagnosisItem {
    pub id: i32,
    pub consult_id: i32,
    pub details: String,
    pub category: Option<DiagnosisCategory>,
}

impl From<Diagnosis> for DiagnosisItem {
    fn from(diagnosis: Diagnosis) -> Self {
        DiagnosisItem {
            id: diagnosis.id,
            consult_id: diagnosis.consult_id,
            details: diagnosis.details,
            category: diagnosis.category,
        }
    }
}

impl ConsultResponse {
    pub fn from(consult: Consult, doctor_nickname: String, mut diagnoses: Vec<Diagnosis>) -> Self {
        diagnoses.sort_by_key(|diagnosis| diagnosis.id);
        ConsultResponse {
            id: consult.id,
            visit_id: consult.visit_id,
            date: consult.date,
            past_medical_history: consult.past_medical_history,
            consultation: consult.consultation,
            treatment_plan: consult.treatment_plan,
            referred_for: consult.referred_for,
            referral_notes: consult.referral_notes,
            remarks: consult.remarks,
            doctor_id: consult.doctor_id,
            doctor_nickname,
            diagnoses: diagnoses.into_iter().map(Into::into).collect(),
        }
    }
}

/// Record a consult with diagnoses and optional prescriptions.
async fn create(ctx: Ctx, Staff(user): Staff, Req(req): Req<CreateRequest>) -> ApiResult<Created<ConsultResponse>> {
    let mut tx = ctx.begin().await?;
    let visit_exists: bool = sqlx::query_scalar(r#"SELECT EXISTS (SELECT 1 FROM "Visits" WHERE "Id" = $1)"#)
        .bind(req.visit_id)
        .fetch_one(&mut *tx)
        .await?;
    if !visit_exists {
        return Err(ApiError::not_found("Visit was not found."));
    }
    let inputs = req.diagnoses.unwrap_or_default();
    if inputs.is_empty() {
        return Err(ApiError::bad_request("At least one diagnosis is required."));
    }

    let orders = req.orders.unwrap_or_default();
    if !orders.is_empty() {
        if orders.iter().any(|order| order.quantity < 1) {
            return Err(ApiError::bad_request("Order quantity must be at least 1."));
        }
        let mut brand_ids: Vec<i32> = orders.iter().map(|order| order.medication_brand_id).collect();
        brand_ids.sort_unstable();
        brand_ids.dedup();
        let found: i64 = sqlx::query_scalar(r#"SELECT COUNT(*) FROM "MedicationBrands" WHERE "Id" = ANY($1)"#)
            .bind(&brand_ids)
            .fetch_one(&mut *tx)
            .await?;
        if found != brand_ids.len() as i64 {
            return Err(ApiError::bad_request("Medication brand was not found."));
        }
    }
    let mut details = Vec::with_capacity(inputs.len());
    for diagnosis in &inputs {
        let text = diagnosis.details.trim();
        if text.is_empty() {
            return Err(ApiError::bad_request("Diagnosis details are required."));
        }
        details.push((text.to_owned(), diagnosis.category));
    }

    let now = Ts::now();
    let consult = tx
        .insert(Consult {
            id: 0,
            date: now,
            past_medical_history: req.past_medical_history,
            consultation: req.consultation,
            treatment_plan: req.treatment_plan,
            referred_for: req.referred_for,
            referral_notes: req.referral_notes,
            remarks: req.remarks,
            doctor_id: user.id,
            visit_id: req.visit_id,
        })
        .await?;
    let mut diagnoses = Vec::with_capacity(details.len());
    for (details, category) in details {
        diagnoses.push(tx.insert(Diagnosis { id: 0, details, category, consult_id: consult.id }).await?);
    }
    for order in orders {
        tx.insert(Order {
            id: 0,
            consult_id: consult.id,
            medication_brand_id: order.medication_brand_id,
            dosage_instructions: order.dosage_instructions.unwrap_or_default(),
            remarks: order.remarks,
            status: OrderStatus::Pending,
            quantity: order.quantity,
            approved_by_id: None,
            approved_at: None,
            created_at: now,
        })
        .await?;
    }
    let nickname: Option<String> = sqlx::query_scalar(r#"SELECT "Nickname" FROM "StaffUsers" WHERE "Id" = $1"#)
        .bind(consult.doctor_id)
        .fetch_optional(&mut *tx)
        .await?;
    let nickname = nickname.ok_or_else(|| ApiError::not_found("Doctor was not found."))?;
    tx.commit().await?;

    Ok(Created::at(format!("/consults/{}", consult.id), ConsultResponse::from(consult, nickname, diagnoses)))
}

pub fn router() -> Router<AppState> {
    Router::new().route("/consults", post(create))
}

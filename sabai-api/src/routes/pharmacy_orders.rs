//! The pharmacy queue: pending orders, grouped by patient and visit.

use std::collections::{BTreeMap, HashMap};

use axum::Router;
use axum::routing::get;
use serde::Serialize;

use crate::auth::Staff;
use crate::domain::{DiagnosisCategory, OrderStatus, Patient, StockStatus};
use crate::error::ApiResult;
use crate::http::{Ctx, Json};
use crate::state::AppState;
use crate::time::Ts;

/// Pending pharmacy work grouped by patient.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PharmacyOrdersResponse {
    pub patient: PatientHeader,
    pub visits: Vec<VisitBundle>,
}

/// Patient header on the pharmacy queue.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PatientHeader {
    pub display_id: String,
    pub name: String,
    pub village_code: Option<String>,
}

/// One visit's pending orders and diagnoses.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct VisitBundle {
    pub visit_id: i32,
    pub visit_date: Ts,
    pub orders: Vec<PharmacyOrderItem>,
    pub diagnoses: Vec<DiagnosisItem>,
}

/// A pending medication order waiting to be dispensed.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PharmacyOrderItem {
    pub order_id: i32,
    pub medication_name: String,
    pub quantity: i32,
    pub is_low_stock: bool,
    pub dosage_instructions: String,
}

/// Diagnosis recorded on one of the visit's consults.
#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct DiagnosisItem {
    pub category: Option<DiagnosisCategory>,
    pub details: String,
}

#[derive(Debug, sqlx::FromRow)]
struct PendingOrder {
    #[sqlx(rename = "Id")]
    id: i32,
    #[sqlx(rename = "Quantity")]
    quantity: i32,
    #[sqlx(rename = "DosageInstructions")]
    dosage_instructions: String,
    #[sqlx(rename = "MedicationName")]
    medication_name: String,
    #[sqlx(rename = "IngredientId")]
    ingredient_id: i32,
    #[sqlx(rename = "FallBelow")]
    fall_below: i32,
    #[sqlx(rename = "VisitId")]
    visit_id: i32,
    #[sqlx(rename = "VisitDate")]
    visit_date: Ts,
    #[sqlx(rename = "PatientId")]
    patient_id: i32,
    #[sqlx(rename = "PatientName")]
    patient_name: String,
    #[sqlx(rename = "VillageCode")]
    village_code: Option<String>,
}

/// List pending pharmacy orders grouped by patient and visit.
async fn list(ctx: Ctx, _: Staff) -> ApiResult<Json<Vec<PharmacyOrdersResponse>>> {
    let pending: Vec<PendingOrder> = sqlx::query_as(
        r#"SELECT o."Id", o."Quantity", o."DosageInstructions", b."Name" AS "MedicationName",
                  b."ActiveIngredientId" AS "IngredientId", i."FallBelow", v."Id" AS "VisitId",
                  v."Date" AS "VisitDate", p."Id" AS "PatientId", p."Name" AS "PatientName",
                  vc."Code" AS "VillageCode"
           FROM "Orders" o
           JOIN "MedicationBrands" b ON b."Id" = o."MedicationBrandId"
           JOIN "MedicationActiveIngredients" i ON i."Id" = b."ActiveIngredientId"
           JOIN "Consults" c ON c."Id" = o."ConsultId"
           JOIN "Visits" v ON v."Id" = c."VisitId"
           JOIN "Patients" p ON p."Id" = v."PatientId"
           LEFT JOIN "VillageCodes" vc ON vc."Id" = p."VillageCodeId"
           WHERE o."Status" = $1
           ORDER BY o."Id""#,
    )
    .bind(OrderStatus::Pending)
    .fetch_all(ctx.db())
    .await?;
    let usable: Vec<PendingOrder> = pending.into_iter().filter(|order| !order.patient_name.trim().is_empty()).collect();

    let mut ingredient_ids: Vec<i32> = usable.iter().map(|order| order.ingredient_id).collect();
    ingredient_ids.sort_unstable();
    ingredient_ids.dedup();
    let mut active_by_ingredient: HashMap<i32, i64> = HashMap::new();
    if !ingredient_ids.is_empty() {
        let totals: Vec<(i32, i64)> = sqlx::query_as(
            r#"SELECT b."ActiveIngredientId", SUM(s."Quantity")::int8
               FROM "MedicationStocks" s
               JOIN "MedicationBrands" b ON b."Id" = s."MedicationBrandId"
               WHERE s."StockStatus" = $1 AND b."ActiveIngredientId" = ANY($2)
               GROUP BY b."ActiveIngredientId""#,
        )
        .bind(StockStatus::Active)
        .bind(&ingredient_ids)
        .fetch_all(ctx.db())
        .await?;
        active_by_ingredient = totals.into_iter().collect();
    }

    let mut visit_ids: Vec<i32> = usable.iter().map(|order| order.visit_id).collect();
    visit_ids.sort_unstable();
    visit_ids.dedup();
    let mut diagnoses: HashMap<i32, Vec<DiagnosisItem>> = HashMap::new();
    if !visit_ids.is_empty() {
        let loaded: Vec<(i32, Option<DiagnosisCategory>, String)> = sqlx::query_as(
            r#"SELECT c."VisitId", d."Category", d."Details"
               FROM "Diagnoses" d
               JOIN "Consults" c ON c."Id" = d."ConsultId"
               WHERE c."VisitId" = ANY($1)
               ORDER BY d."Id""#,
        )
        .bind(&visit_ids)
        .fetch_all(ctx.db())
        .await?;
        for (visit_id, category, details) in loaded {
            diagnoses.entry(visit_id).or_default().push(DiagnosisItem { category, details });
        }
    }

    // Patients in id order; each patient's rows stay in order id order.
    let mut by_patient: BTreeMap<i32, Vec<PendingOrder>> = BTreeMap::new();
    for order in usable {
        by_patient.entry(order.patient_id).or_default().push(order);
    }

    let grouped = by_patient
        .into_iter()
        .map(|(patient_id, orders)| {
            let header = &orders[0];
            let patient = PatientHeader {
                display_id: Patient::format_display_id(header.village_code.as_deref(), patient_id),
                name: header.patient_name.clone(),
                village_code: header.village_code.clone(),
            };

            let mut by_visit: BTreeMap<i32, VisitBundle> = BTreeMap::new();
            for order in orders {
                let bundle = by_visit.entry(order.visit_id).or_insert_with(|| VisitBundle {
                    visit_id: order.visit_id,
                    visit_date: order.visit_date,
                    orders: Vec::new(),
                    diagnoses: diagnoses.get(&order.visit_id).cloned().unwrap_or_default(),
                });
                bundle.orders.push(PharmacyOrderItem {
                    order_id: order.id,
                    medication_name: order.medication_name,
                    quantity: order.quantity,
                    is_low_stock: i64::from(order.fall_below)
                        > active_by_ingredient.get(&order.ingredient_id).copied().unwrap_or(0),
                    dosage_instructions: order.dosage_instructions,
                });
            }
            // Oldest visit first; visits of the same instant in id order.
            let mut visits: Vec<VisitBundle> = by_visit.into_values().collect();
            visits.sort_by_key(|visit| visit.visit_date);
            PharmacyOrdersResponse { patient, visits }
        })
        .collect();
    Ok(Json(grouped))
}

pub fn router() -> Router<AppState> {
    Router::new().route("/pharmacy-orders", get(list))
}

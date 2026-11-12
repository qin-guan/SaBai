//! Medication active ingredients: what a medicine is made of, with its low-stock threshold.

use axum::Router;
use axum::routing::{get, put};
use serde::{Deserialize, Serialize};

use crate::auth::Staff;
use crate::domain::MedicationActiveIngredient;
use crate::error::{ApiError, ApiResult, OrNotFound};
use crate::http::{Created, Ctx, Json, NoContent, Req, Rules, Validate};
use crate::state::AppState;

const NOT_FOUND: &str = "Medication ingredient was not found.";

/// Medication active ingredient in catalog order.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct IngredientResponse {
    pub id: i32,
    pub name: String,
    pub unit_of_measurement: String,
    pub fall_below: i32,
    pub remarks: Option<String>,
}

impl From<MedicationActiveIngredient> for IngredientResponse {
    fn from(ingredient: MedicationActiveIngredient) -> Self {
        IngredientResponse {
            id: ingredient.id,
            name: ingredient.name,
            unit_of_measurement: ingredient.unit_of_measurement,
            fall_below: ingredient.fall_below,
            remarks: ingredient.remarks,
        }
    }
}

/// Trimmed text, or nothing when it is absent or blank.
fn non_blank(text: Option<&str>) -> Option<String> {
    text.map(str::trim).filter(|text| !text.is_empty()).map(str::to_owned)
}

async fn find(conn: &mut sqlx::PgConnection, id: i32) -> ApiResult<MedicationActiveIngredient> {
    sqlx::query_as(r#"SELECT * FROM "MedicationActiveIngredients" WHERE "Id" = $1"#)
        .bind(id)
        .fetch_optional(conn)
        .await
        .or_not_found(NOT_FOUND)
}

/// List medication active ingredients.
async fn list(ctx: Ctx, _: Staff) -> ApiResult<Json<Vec<IngredientResponse>>> {
    let ingredients: Vec<MedicationActiveIngredient> =
        sqlx::query_as(r#"SELECT * FROM "MedicationActiveIngredients" ORDER BY "Id""#).fetch_all(ctx.db()).await?;
    Ok(Json(ingredients.into_iter().map(Into::into).collect()))
}

/// New medication active ingredient.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateRequest {
    pub name: String,
    pub unit_of_measurement: String,
    #[serde(default)]
    pub fall_below: i32,
    pub remarks: Option<String>,
}

impl Validate for CreateRequest {
    fn validate(&self, v: &mut Rules) {
        v.not_empty("name", &self.name).max_len("name", &self.name, 255);
        v.not_empty("unitOfMeasurement", &self.unit_of_measurement).max_len(
            "unitOfMeasurement",
            &self.unit_of_measurement,
            255,
        );
        v.greater_than("fallBelow", self.fall_below, 0);
    }
}

/// Create a medication active ingredient.
async fn create(ctx: Ctx, _: Staff, Req(req): Req<CreateRequest>) -> ApiResult<Created<IngredientResponse>> {
    let mut tx = ctx.begin().await?;
    let ingredient = tx
        .insert(MedicationActiveIngredient {
            id: 0,
            name: req.name.trim().to_owned(),
            unit_of_measurement: req.unit_of_measurement.trim().to_owned(),
            fall_below: req.fall_below,
            remarks: non_blank(req.remarks.as_deref()),
        })
        .await?;
    tx.commit().await?;
    Ok(Created::at("/medication-ingredients", ingredient.into()))
}

/// Full replacement of a medication active ingredient.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateRequest {
    #[serde(default)]
    pub id: i32,
    pub name: String,
    pub unit_of_measurement: String,
    #[serde(default)]
    pub fall_below: i32,
    pub remarks: Option<String>,
}

impl Validate for UpdateRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("id", self.id, 0);
        v.not_empty("name", &self.name).max_len("name", &self.name, 255);
        v.not_empty("unitOfMeasurement", &self.unit_of_measurement).max_len(
            "unitOfMeasurement",
            &self.unit_of_measurement,
            255,
        );
        v.greater_than("fallBelow", self.fall_below, 0);
    }
}

/// Replace a medication active ingredient.
async fn update(ctx: Ctx, _: Staff, Req(req): Req<UpdateRequest>) -> ApiResult<Json<IngredientResponse>> {
    let mut tx = ctx.begin().await?;
    let before = find(&mut tx, req.id).await?;
    let mut ingredient = before.clone();
    ingredient.name = req.name.trim().to_owned();
    ingredient.unit_of_measurement = req.unit_of_measurement.trim().to_owned();
    ingredient.fall_below = req.fall_below;
    ingredient.remarks = non_blank(req.remarks.as_deref());
    let ingredient = tx.update(&before, ingredient).await?;
    tx.commit().await?;
    Ok(Json(ingredient.into()))
}

/// Identifies the medication active ingredient to delete.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DeleteRequest {
    #[serde(default)]
    pub id: i32,
}

impl Validate for DeleteRequest {}

/// Delete a medication active ingredient.
async fn delete(ctx: Ctx, _: Staff, Req(req): Req<DeleteRequest>) -> ApiResult<NoContent> {
    let mut tx = ctx.begin().await?;
    let ingredient = find(&mut tx, req.id).await?;
    let in_use: bool =
        sqlx::query_scalar(r#"SELECT EXISTS (SELECT 1 FROM "MedicationBrands" WHERE "ActiveIngredientId" = $1)"#)
            .bind(ingredient.id)
            .fetch_one(&mut *tx)
            .await?;
    if in_use {
        return Err(ApiError::conflict("The ingredient is referenced by a medication brand."));
    }
    tx.delete(&ingredient).await?;
    tx.commit().await?;
    Ok(NoContent)
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/medication-ingredients", get(list).post(create))
        .route("/medication-ingredients/{id}", put(update).delete(delete))
}

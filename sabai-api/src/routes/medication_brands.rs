//! Medication brands: the products the pharmacy stocks, each made of one active ingredient.

use axum::Router;
use axum::routing::{delete, get};
use serde::{Deserialize, Deserializer, Serialize};

use crate::auth::Staff;
use crate::domain::MedicationBrand;
use crate::error::{ApiError, ApiResult, OrNotFound};
use crate::http::{Created, Ctx, Json, NoContent, Req, Rules, Validate};
use crate::state::AppState;

const NOT_FOUND: &str = "Medication brand was not found.";
const INGREDIENT_NOT_FOUND: &str = "Medication ingredient was not found.";

/// Medication brand catalog row.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct BrandResponse {
    pub id: i32,
    pub name: String,
    pub active_ingredient_id: i32,
    pub remarks: Option<String>,
}

impl From<MedicationBrand> for BrandResponse {
    fn from(brand: MedicationBrand) -> Self {
        BrandResponse {
            id: brand.id,
            name: brand.name,
            active_ingredient_id: brand.active_ingredient_id,
            remarks: brand.remarks,
        }
    }
}

/// Remarks must be sent, but may be null (which validation then rejects) or empty.
fn nullable<'de, D: Deserializer<'de>>(deserializer: D) -> Result<Option<String>, D::Error> {
    Option::<String>::deserialize(deserializer)
}

async fn find(conn: &mut sqlx::PgConnection, id: i32) -> ApiResult<MedicationBrand> {
    sqlx::query_as(r#"SELECT * FROM "MedicationBrands" WHERE "Id" = $1"#)
        .bind(id)
        .fetch_optional(conn)
        .await
        .or_not_found(NOT_FOUND)
}

async fn require_ingredient(conn: &mut sqlx::PgConnection, id: i32) -> ApiResult<()> {
    let exists: bool =
        sqlx::query_scalar(r#"SELECT EXISTS (SELECT 1 FROM "MedicationActiveIngredients" WHERE "Id" = $1)"#)
            .bind(id)
            .fetch_one(conn)
            .await?;
    if exists { Ok(()) } else { Err(ApiError::not_found(INGREDIENT_NOT_FOUND)) }
}

/// List medication brands.
async fn list(ctx: Ctx, _: Staff) -> ApiResult<Json<Vec<BrandResponse>>> {
    let brands: Vec<MedicationBrand> =
        sqlx::query_as(r#"SELECT * FROM "MedicationBrands" ORDER BY "Id""#).fetch_all(ctx.db()).await?;
    Ok(Json(brands.into_iter().map(Into::into).collect()))
}

/// Medication brand joined to its active ingredient name.
#[derive(Debug, Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
pub struct BrandWithIngredientResponse {
    #[sqlx(rename = "Id")]
    pub id: i32,
    #[sqlx(rename = "Name")]
    pub name: String,
    #[sqlx(rename = "ActiveIngredientName")]
    pub active_ingredient_name: String,
}

/// List medication brands with ingredient names.
async fn list_with_ingredient(ctx: Ctx, _: Staff) -> ApiResult<Json<Vec<BrandWithIngredientResponse>>> {
    let brands = sqlx::query_as(
        r#"SELECT b."Id", b."Name", i."Name" AS "ActiveIngredientName"
           FROM "MedicationBrands" b
           JOIN "MedicationActiveIngredients" i ON i."Id" = b."ActiveIngredientId"
           ORDER BY b."Id""#,
    )
    .fetch_all(ctx.db())
    .await?;
    Ok(Json(brands))
}

/// New medication brand. Remarks may be an empty string.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateRequest {
    pub name: String,
    #[serde(default)]
    pub active_ingredient_id: i32,
    #[serde(deserialize_with = "nullable")]
    pub remarks: Option<String>,
}

impl Validate for CreateRequest {
    fn validate(&self, v: &mut Rules) {
        v.not_empty("name", &self.name).max_len("name", &self.name, 255);
        v.greater_than("activeIngredientId", self.active_ingredient_id, 0);
        v.not_null("remarks", &self.remarks);
    }
}

/// Create a medication brand.
async fn create(ctx: Ctx, _: Staff, Req(req): Req<CreateRequest>) -> ApiResult<Created<BrandResponse>> {
    let mut tx = ctx.begin().await?;
    require_ingredient(&mut tx, req.active_ingredient_id).await?;
    let brand = tx
        .insert(MedicationBrand {
            id: 0,
            name: req.name.trim().to_owned(),
            active_ingredient_id: req.active_ingredient_id,
            remarks: req.remarks.map(|remarks| remarks.trim().to_owned()),
        })
        .await?;
    tx.commit().await?;
    Ok(Created::at("/medication-brands", brand.into()))
}

/// Partial brand update. Name may be omitted; remarks may be empty.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateRequest {
    #[serde(default)]
    pub id: i32,
    pub name: Option<String>,
    #[serde(default)]
    pub active_ingredient_id: i32,
    #[serde(deserialize_with = "nullable")]
    pub remarks: Option<String>,
}

impl Validate for UpdateRequest {
    fn validate(&self, v: &mut Rules) {
        v.greater_than("id", self.id, 0);
        if self.name.is_some() {
            v.not_empty("name", &self.name).max_len("name", &self.name, 255);
        }
        v.greater_than("activeIngredientId", self.active_ingredient_id, 0);
        v.not_null("remarks", &self.remarks);
    }
}

/// Update a medication brand.
async fn update(ctx: Ctx, _: Staff, Req(req): Req<UpdateRequest>) -> ApiResult<Json<BrandResponse>> {
    let mut tx = ctx.begin().await?;
    let before = find(&mut tx, req.id).await?;
    require_ingredient(&mut tx, req.active_ingredient_id).await?;
    let mut brand = before.clone();
    if let Some(name) = &req.name {
        brand.name = name.trim().to_owned();
    }
    brand.active_ingredient_id = req.active_ingredient_id;
    brand.remarks = req.remarks.map(|remarks| remarks.trim().to_owned());
    let brand = tx.update(&before, brand).await?;
    tx.commit().await?;
    Ok(Json(brand.into()))
}

/// Identifies the medication brand to delete.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DeleteRequest {
    #[serde(default)]
    pub id: i32,
}

impl Validate for DeleteRequest {}

/// Delete a medication brand.
async fn remove(ctx: Ctx, _: Staff, Req(req): Req<DeleteRequest>) -> ApiResult<NoContent> {
    let mut tx = ctx.begin().await?;
    let brand = find(&mut tx, req.id).await?;
    let has_stock: bool =
        sqlx::query_scalar(r#"SELECT EXISTS (SELECT 1 FROM "MedicationStocks" WHERE "MedicationBrandId" = $1)"#)
            .bind(brand.id)
            .fetch_one(&mut *tx)
            .await?;
    if has_stock {
        return Err(ApiError::conflict("The brand is referenced by medication stock."));
    }
    let has_orders: bool =
        sqlx::query_scalar(r#"SELECT EXISTS (SELECT 1 FROM "Orders" WHERE "MedicationBrandId" = $1)"#)
            .bind(brand.id)
            .fetch_one(&mut *tx)
            .await?;
    if has_orders {
        return Err(ApiError::conflict("The brand is referenced by an order."));
    }
    tx.delete(&brand).await?;
    tx.commit().await?;
    Ok(NoContent)
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/medication-brands", get(list).post(create).patch(update))
        .route("/medication-brands/with-ingredient", get(list_with_ingredient))
        .route("/medication-brands/{id}", delete(remove).patch(update))
}

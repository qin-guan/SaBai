//! Village codes: the villages the clinic visits, used to prefix patient ids.

use std::sync::LazyLock;

use axum::Router;
use axum::routing::get;
use regex::Regex;
use serde::{Deserialize, Serialize};

use crate::auth::Staff;
use crate::domain::VillageCode;
use crate::error::{ApiError, ApiResult, OrNotFound};
use crate::http::{Created, Ctx, Json, NoContent, Req, Rules, Validate};
use crate::state::AppState;
use crate::time::Ts;

static COLOR_HEX: LazyLock<Regex> = LazyLock::new(|| Regex::new("^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$").unwrap());

const NOT_FOUND: &str = "Village code was not found.";

/// A village code.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct VillageCodeResponse {
    pub id: i32,
    pub code: String,
    pub name: String,
    pub color_hex: String,
    pub is_visible: bool,
    pub created_at: Ts,
    pub updated_at: Ts,
}

impl From<VillageCode> for VillageCodeResponse {
    fn from(village: VillageCode) -> Self {
        VillageCodeResponse {
            id: village.id,
            code: village.code,
            name: village.name,
            color_hex: village.color_hex,
            is_visible: village.is_visible,
            created_at: village.created_at,
            updated_at: village.updated_at,
        }
    }
}

/// Identifies a village code.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct IdRequest {
    #[serde(default)]
    pub id: i32,
}

impl Validate for IdRequest {}

async fn find(conn: &mut sqlx::PgConnection, id: i32) -> ApiResult<VillageCode> {
    sqlx::query_as(r#"SELECT * FROM "VillageCodes" WHERE "Id" = $1"#)
        .bind(id)
        .fetch_optional(conn)
        .await
        .or_not_found(NOT_FOUND)
}

/// Optional filter for the village code list.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ListRequest {
    #[serde(default)]
    pub include_hidden: bool,
}

impl Validate for ListRequest {}

/// Village codes, newest first. Hidden codes are omitted unless requested.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ListResponse {
    pub village_codes: Vec<VillageCodeResponse>,
}

/// List village codes.
async fn list(ctx: Ctx, _: Staff, Req(req): Req<ListRequest>) -> ApiResult<Json<ListResponse>> {
    let codes: Vec<VillageCode> = sqlx::query_as(
        r#"SELECT * FROM "VillageCodes" WHERE ($1 OR "IsVisible") ORDER BY "CreatedAt" DESC, "Id" DESC"#,
    )
    .bind(req.include_hidden)
    .fetch_all(ctx.db())
    .await?;
    Ok(Json(ListResponse { village_codes: codes.into_iter().map(Into::into).collect() }))
}

/// Get a village code.
async fn get_one(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<Json<VillageCodeResponse>> {
    let mut conn = ctx.db().acquire().await?;
    Ok(Json(find(&mut conn, req.id).await?.into()))
}

/// Payload for creating a village code.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateRequest {
    pub code: String,
    pub name: String,
    pub color_hex: String,
    #[serde(default = "visible")]
    pub is_visible: bool,
}

fn visible() -> bool {
    true
}

impl Validate for CreateRequest {
    fn validate(&self, v: &mut Rules) {
        v.not_empty("code", &self.code).max_len("code", &self.code, 255);
        v.not_empty("name", &self.name).max_len("name", &self.name, 255);
        v.not_empty("colorHex", &self.color_hex).matches("colorHex", &self.color_hex, &COLOR_HEX);
    }
}

/// Create a village code.
async fn create(ctx: Ctx, _: Staff, Req(req): Req<CreateRequest>) -> ApiResult<Created<VillageCodeResponse>> {
    let code = req.code.trim();
    let mut tx = ctx.begin().await?;
    let taken: bool = sqlx::query_scalar(r#"SELECT EXISTS (SELECT 1 FROM "VillageCodes" WHERE "Code" = $1)"#)
        .bind(code)
        .fetch_one(&mut *tx)
        .await?;
    if taken {
        return Err(ApiError::conflict("A village code with this code already exists."));
    }

    let now = Ts::now();
    let village = tx
        .insert(VillageCode {
            id: 0,
            code: code.to_owned(),
            name: req.name.trim().to_owned(),
            color_hex: req.color_hex.trim().to_owned(),
            is_visible: req.is_visible,
            created_at: now,
            updated_at: now,
        })
        .await?;
    tx.commit().await?;
    Ok(Created::at(format!("/village-codes/{}", village.id), village.into()))
}

/// Partial update for a village code. Omitted fields are left unchanged.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateRequest {
    #[serde(default)]
    pub id: i32,
    pub code: Option<String>,
    pub name: Option<String>,
    pub color_hex: Option<String>,
    pub is_visible: Option<bool>,
}

impl Validate for UpdateRequest {
    fn validate(&self, v: &mut Rules) {
        if self.code.is_some() {
            v.not_empty("code", &self.code).max_len("code", &self.code, 255);
        }
        if self.name.is_some() {
            v.not_empty("name", &self.name).max_len("name", &self.name, 255);
        }
        v.matches("colorHex", &self.color_hex, &COLOR_HEX);
    }
}

/// Update a village code.
async fn update(ctx: Ctx, _: Staff, Req(req): Req<UpdateRequest>) -> ApiResult<Json<VillageCodeResponse>> {
    let mut tx = ctx.begin().await?;
    let before = find(&mut tx, req.id).await?;
    let mut village = before.clone();

    if let Some(code) = &req.code {
        let code = code.trim();
        let taken: bool = sqlx::query_scalar(
            r#"SELECT EXISTS (SELECT 1 FROM "VillageCodes" WHERE "Id" <> $1 AND "Code" = $2)"#,
        )
        .bind(village.id)
        .bind(code)
        .fetch_one(&mut *tx)
        .await?;
        if taken {
            return Err(ApiError::conflict("A village code with this code already exists."));
        }
        village.code = code.to_owned();
    }
    if let Some(name) = &req.name {
        village.name = name.trim().to_owned();
    }
    if let Some(color_hex) = &req.color_hex {
        village.color_hex = color_hex.trim().to_owned();
    }
    if let Some(is_visible) = req.is_visible {
        village.is_visible = is_visible;
    }
    village.updated_at = Ts::now();

    let village = tx.update(&before, village).await?;
    tx.commit().await?;
    Ok(Json(village.into()))
}

/// Delete a village code.
async fn delete(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<NoContent> {
    let mut tx = ctx.begin().await?;
    let village = find(&mut tx, req.id).await?;
    let in_use: bool = sqlx::query_scalar(r#"SELECT EXISTS (SELECT 1 FROM "Visits" WHERE "VillageCodeId" = $1)"#)
        .bind(village.id)
        .fetch_one(&mut *tx)
        .await?;
    if in_use {
        return Err(ApiError::conflict("A visit still references this village code."));
    }
    tx.delete(&village).await?;
    tx.commit().await?;
    Ok(NoContent)
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/village-codes", get(list).post(create))
        .route("/village-codes/{id}", get(get_one).patch(update).delete(delete))
}

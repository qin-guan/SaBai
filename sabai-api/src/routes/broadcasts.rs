//! Broadcasts: clinic-wide notices shown to staff.

use axum::Router;
use axum::routing::{delete, get, post};
use serde::{Deserialize, Serialize};

use crate::auth::Staff;
use crate::domain::{Broadcast, BroadcastSeverity};
use crate::error::{ApiResult, OrNotFound};
use crate::http::{Created, Ctx, Json, NoContent, Req, Rules, Validate};
use crate::state::AppState;
use crate::time::Ts;

pub const DEFAULT_LIMIT: i32 = 50;
pub const MAX_LIMIT: i32 = 200;

/// A clinic broadcast. `publishedAt` is set only once.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct BroadcastResponse {
    pub id: i32,
    pub title: String,
    pub body: String,
    pub severity: BroadcastSeverity,
    pub created_by: Option<String>,
    pub created_at: Ts,
    pub published_at: Option<Ts>,
    pub attempts: i32,
}

impl From<Broadcast> for BroadcastResponse {
    fn from(broadcast: Broadcast) -> Self {
        BroadcastResponse {
            id: broadcast.id,
            title: broadcast.title,
            body: broadcast.body,
            severity: broadcast.severity,
            created_by: broadcast.created_by,
            created_at: broadcast.created_at,
            published_at: broadcast.published_at,
            attempts: broadcast.attempts,
        }
    }
}

/// Identifies a broadcast.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct IdRequest {
    #[serde(default)]
    pub id: i32,
}

impl Validate for IdRequest {}

async fn find(conn: &mut sqlx::PgConnection, id: i32) -> ApiResult<Broadcast> {
    sqlx::query_as(r#"SELECT * FROM "Broadcasts" WHERE "Id" = $1"#)
        .bind(id)
        .fetch_optional(conn)
        .await
        .or_not_found("Broadcast was not found.")
}

/// New unpublished broadcast. Severity defaults to info.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateRequest {
    pub title: String,
    pub body: String,
    #[serde(default = "info")]
    pub severity: BroadcastSeverity,
}

fn info() -> BroadcastSeverity {
    BroadcastSeverity::Info
}

impl Validate for CreateRequest {
    fn validate(&self, v: &mut Rules) {
        v.not_empty("title", &self.title).max_len("title", &self.title, 255);
        v.not_empty("body", &self.body);
    }
}

/// Create a broadcast.
async fn create(
    ctx: Ctx,
    Staff(staff): Staff,
    Req(req): Req<CreateRequest>,
) -> ApiResult<Created<BroadcastResponse>> {
    let mut tx = ctx.begin().await?;
    let broadcast = tx
        .insert(Broadcast {
            id: 0,
            title: req.title.trim().to_owned(),
            body: req.body.trim().to_owned(),
            severity: req.severity,
            created_by: Some(staff.email_or_name().to_owned()),
            created_at: Ts::now(),
            published_at: None,
            attempts: 0,
        })
        .await?;
    tx.commit().await?;
    Ok(Created::at("/broadcasts", broadcast.into()))
}

/// Broadcast list paging. Limit defaults to 50 (max 200); Since returns only ids above it.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ListRequest {
    pub limit: Option<i32>,
    pub since: Option<i32>,
}

impl Validate for ListRequest {
    fn validate(&self, v: &mut Rules) {
        v.between("limit", self.limit, 1, MAX_LIMIT);
    }
}

/// List broadcasts, newest first.
async fn list(ctx: Ctx, _: Staff, Req(req): Req<ListRequest>) -> ApiResult<Json<Vec<BroadcastResponse>>> {
    let broadcasts: Vec<Broadcast> = sqlx::query_as(
        r#"SELECT * FROM "Broadcasts" WHERE ($1::int IS NULL OR "Id" > $1) ORDER BY "Id" DESC LIMIT $2"#,
    )
    .bind(req.since)
    .bind(i64::from(req.limit.unwrap_or(DEFAULT_LIMIT)))
    .fetch_all(ctx.db())
    .await?;
    Ok(Json(broadcasts.into_iter().map(Into::into).collect()))
}

/// Delete a broadcast.
async fn remove(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<NoContent> {
    let mut tx = ctx.begin().await?;
    let broadcast = find(&mut tx, req.id).await?;
    tx.delete(&broadcast).await?;
    tx.commit().await?;
    Ok(NoContent)
}

/// Publish a broadcast.
async fn publish(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<Json<BroadcastResponse>> {
    let mut tx = ctx.begin().await?;
    let mut broadcast = find(&mut tx, req.id).await?;
    if broadcast.published_at.is_none() {
        let before = broadcast.clone();
        broadcast.published_at = Some(Ts::now());
        broadcast = tx.update(&before, broadcast).await?;
        tx.commit().await?;
    }
    Ok(Json(broadcast.into()))
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/broadcasts", get(list).post(create))
        .route("/broadcasts/{id}", delete(remove))
        .route("/broadcasts/{id}/publish", post(publish))
}

//! The audit log, for admins.

use axum::Router;
use axum::routing::get;
use serde::{Deserialize, Serialize};
use sqlx::{Postgres, QueryBuilder};
use uuid::Uuid;

use crate::auth::Admin;
use crate::db::AuditAction;
use crate::db::audit::FieldChange;
use crate::error::ApiResult;
use crate::http::{Ctx, Json, Req, Rules, Validate};
use crate::state::AppState;
use crate::time::Ts;

pub const DEFAULT_TAKE: i32 = 50;
pub const MAX_TAKE: i32 = 200;

/// Audit log filters. All are optional and combine with AND.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ListRequest {
    /// Entity type, e.g. `Patient` or `MedicationStock`.
    pub entity_type: Option<String>,
    /// Primary key of one row; use with `entityType`.
    pub entity_id: Option<String>,
    /// Only events caused by this staff user.
    pub user_id: Option<Uuid>,
    pub action: Option<AuditAction>,
    /// Only events at or after this time.
    pub from: Option<Ts>,
    /// Only events before this time.
    pub to: Option<Ts>,
    /// Paging cursor: return entries older than this id (the previous page's `nextBefore`).
    pub before: Option<i32>,
    /// Page size, 1–200. Defaults to 50.
    pub take: Option<i32>,
}

impl Validate for ListRequest {
    fn validate(&self, v: &mut Rules) {
        v.between("take", self.take, 1, MAX_TAKE);
        if let (Some(from), Some(to)) = (self.from, self.to) {
            v.must("to", to > from, "'to' must be after 'from'.");
        }
    }
}

/// A page of audit entries, newest first.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ListResponse {
    pub entries: Vec<AuditLogEntryResponse>,
    /// Pass as `before` to get the next (older) page; null on the last page.
    pub next_before: Option<i32>,
}

#[derive(Debug, Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
pub struct AuditLogEntryResponse {
    #[sqlx(rename = "Id")]
    pub id: i32,
    #[sqlx(rename = "OccurredAt")]
    pub occurred_at: Ts,
    #[sqlx(rename = "Action")]
    pub action: AuditAction,
    #[sqlx(rename = "UserId")]
    pub user_id: Option<Uuid>,
    #[sqlx(rename = "UserName")]
    pub user_name: Option<String>,
    #[sqlx(rename = "EntityType")]
    pub entity_type: Option<String>,
    #[sqlx(rename = "EntityId")]
    pub entity_id: Option<String>,
    #[sqlx(rename = "Changes", json(nullable))]
    #[serde(serialize_with = "changes")]
    pub changes: Option<Vec<FieldChange>>,
    #[sqlx(rename = "Details")]
    pub details: Option<String>,
    #[sqlx(rename = "IpAddress")]
    pub ip_address: Option<String>,
    #[sqlx(rename = "Request")]
    pub request: Option<String>,
}

/// `[{"field","old","new"}]`; an entry without changes is an empty list.
fn changes<S: serde::Serializer>(value: &Option<Vec<FieldChange>>, serializer: S) -> Result<S::Ok, S::Error> {
    #[derive(Serialize)]
    struct Change<'a> {
        field: &'a str,
        old: &'a Option<String>,
        new: &'a Option<String>,
    }
    serializer.collect_seq(
        value.iter().flatten().map(|change| Change { field: &change.field, old: &change.old, new: &change.new }),
    )
}

/// List audit log entries.
///
/// Who created, changed or deleted which record, and staff sign-ins, newest first.
/// Admin only, because entries include the old and new values of patient records.
/// Page with `before` set to the previous page's `nextBefore`.
async fn list(ctx: Ctx, _: Admin, Req(req): Req<ListRequest>) -> ApiResult<Json<ListResponse>> {
    let mut query = QueryBuilder::<Postgres>::new(r#"SELECT * FROM "AuditLogs" WHERE TRUE"#);
    if let Some(entity_type) = req.entity_type.as_deref().map(str::trim).filter(|value| !value.is_empty()) {
        query.push(r#" AND "EntityType" = "#).push_bind(entity_type.to_owned());
    }
    if let Some(entity_id) = req.entity_id.as_deref().map(str::trim).filter(|value| !value.is_empty()) {
        query.push(r#" AND "EntityId" = "#).push_bind(entity_id.to_owned());
    }
    if let Some(user_id) = req.user_id {
        query.push(r#" AND "UserId" = "#).push_bind(user_id);
    }
    if let Some(action) = req.action {
        query.push(r#" AND "Action" = "#).push_bind(action);
    }
    if let Some(from) = req.from {
        query.push(r#" AND "OccurredAt" >= "#).push_bind(from);
    }
    if let Some(to) = req.to {
        query.push(r#" AND "OccurredAt" < "#).push_bind(to);
    }
    if let Some(before) = req.before {
        query.push(r#" AND "Id" < "#).push_bind(before);
    }
    let take = req.take.unwrap_or(DEFAULT_TAKE);
    // Ids increase with time, so they order the log and make a stable cursor.
    query.push(r#" ORDER BY "Id" DESC LIMIT "#).push_bind(i64::from(take) + 1);

    let mut entries: Vec<AuditLogEntryResponse> = query.build_query_as().fetch_all(ctx.db()).await?;
    let has_more = entries.len() > take as usize;
    entries.truncate(take as usize);
    let next_before = if has_more { entries.last().map(|entry| entry.id) } else { None };
    Ok(Json(ListResponse { entries, next_before }))
}

pub fn router() -> Router<AppState> {
    Router::new().route("/audit-logs", get(list))
}

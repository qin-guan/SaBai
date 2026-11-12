//! The audit log: who created, changed or deleted which row, and staff sign-ins.
//!
//! Row changes are logged by [`Tx`](super::Tx) in the same transaction as the change.
//! Rows are append-only and keep the actor's name as it was at the time (there is no
//! foreign key to `StaffUsers` on purpose).

use std::net::IpAddr;

use serde::{Deserialize, Serialize};
use sqlx::PgConnection;
use uuid::Uuid;

use super::entity::{Entity, Kind};
use crate::time::Ts;
use crate::wire_enum;

wire_enum! {
    /// What an audit entry records.
    pub enum AuditAction {
        /// A row was inserted.
        Created = "Created",
        /// One or more columns of a row changed.
        Updated = "Updated",
        /// A row was removed.
        Deleted = "Deleted",
        /// A staff user finished signing in and got a session.
        SignedIn = "SignedIn",
        /// A sign-in step was rejected (wrong password, code, or locked account).
        SignInFailed = "SignInFailed",
        /// A staff user ended their session.
        SignedOut = "SignedOut",
    }
}

/// One column's value before and after. `old` is null on create, `new` on delete.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct FieldChange {
    #[serde(rename = "Field")]
    pub field: String,
    #[serde(rename = "Old")]
    pub old: Option<String>,
    #[serde(rename = "New")]
    pub new: Option<String>,
}

/// Longest value kept per column; longer text is cut with an ellipsis.
pub const MAX_VALUE_LENGTH: usize = 2000;
pub const REDACTED: &str = "[redacted]";
/// Longest attempted username kept for a failed sign-in.
const MAX_USER_NAME_LENGTH: usize = 150;

/// Who is acting in the current request.
#[derive(Debug, Clone, Default)]
pub struct AuditActor {
    pub user_id: Option<Uuid>,
    pub user_name: Option<String>,
    pub ip_address: Option<String>,
    /// HTTP method and path, e.g. `POST /village-codes`.
    pub request: Option<String>,
}

impl AuditActor {
    /// Work done outside a request (startup seeding): no user, no request.
    pub fn system() -> Self {
        AuditActor::default()
    }

    pub fn for_request(method: &str, path: &str, ip: Option<IpAddr>) -> Self {
        AuditActor {
            user_id: None,
            user_name: None,
            ip_address: ip.map(|ip| ip.to_string()),
            request: Some(truncate_chars(&format!("{method} {path}"), 500)),
        }
    }

    pub fn with_user(mut self, id: Uuid, name: &str) -> Self {
        self.user_id = Some(id);
        self.user_name = Some(name.to_owned());
        self
    }
}

pub(super) fn truncate_chars(text: &str, max: usize) -> String {
    match text.char_indices().nth(max) {
        Some((index, _)) => text[..index].to_owned(),
        None => text.to_owned(),
    }
}

fn clip(value: Option<String>) -> Option<String> {
    value.map(|text| {
        if text.chars().count() > MAX_VALUE_LENGTH {
            format!("{}…", truncate_chars(&text, MAX_VALUE_LENGTH - 1))
        } else {
            text
        }
    })
}

fn shown(value: Option<String>, kind: Kind) -> Option<String> {
    if kind == Kind::Secret { value.map(|_| REDACTED.to_owned()) } else { clip(value) }
}

/// The row's primary key; composite keys are comma-separated.
pub(super) fn entity_id<E: Entity>(entity: &E) -> String {
    let values = entity.values();
    E::FIELDS
        .iter()
        .zip(values)
        .filter(|(field, _)| field.kind.is_key())
        .map(|(_, value)| value.audit().unwrap_or_default())
        .collect::<Vec<_>>()
        .join(",")
}

/// Field order of an entry's changes: key columns first, then by name.
fn ordered(mut changes: Vec<(usize, bool, FieldChange)>) -> Vec<FieldChange> {
    changes.sort_by(|a, b| match (a.1, b.1) {
        (true, true) => a.0.cmp(&b.0),
        (true, false) => std::cmp::Ordering::Less,
        (false, true) => std::cmp::Ordering::Greater,
        (false, false) => a.2.field.as_bytes().cmp(b.2.field.as_bytes()),
    });
    changes.into_iter().map(|(_, _, change)| change).collect()
}

/// Every non-null column of a new (`created`) or removed row. Key columns are in the
/// entry's entity id instead, except on join rows, where they show the link.
pub(super) fn snapshot<E: Entity>(entity: &E, created: bool) -> Vec<FieldChange> {
    let mut changes = Vec::new();
    for (index, (field, value)) in E::FIELDS.iter().zip(entity.values()).enumerate() {
        if matches!(field.kind, Kind::Unaudited | Kind::Pk | Kind::PkAuto) {
            continue;
        }
        let Some(text) = value.audit() else { continue };
        let text = shown(Some(text), field.kind);
        let (old, new) = if created { (None, text) } else { (text, None) };
        changes.push((index, field.kind.is_key(), FieldChange { field: field.name.to_owned(), old, new }));
    }
    ordered(changes)
}

/// Columns that differ between two versions of a row.
pub(super) fn diff<E: Entity>(before: &E, after: &E) -> Vec<FieldChange> {
    let mut changes = Vec::new();
    for (index, (field, (old, new))) in
        E::FIELDS.iter().zip(before.values().into_iter().zip(after.values())).enumerate()
    {
        if field.kind == Kind::Unaudited || old.same(new) {
            continue;
        }
        changes.push((
            index,
            field.kind.is_key(),
            FieldChange {
                field: field.name.to_owned(),
                old: shown(old.audit(), field.kind),
                new: shown(new.audit(), field.kind),
            },
        ));
    }
    ordered(changes)
}

pub(super) struct NewEntry<'a> {
    pub action: AuditAction,
    pub user_id: Option<Uuid>,
    pub user_name: Option<&'a str>,
    pub entity_type: Option<&'a str>,
    pub entity_id: Option<&'a str>,
    pub changes: &'a [FieldChange],
    pub details: Option<&'a str>,
}

pub(super) async fn write(
    conn: &mut PgConnection,
    actor: &AuditActor,
    entry: NewEntry<'_>,
) -> Result<(), sqlx::Error> {
    sqlx::query(
        r#"INSERT INTO "AuditLogs"
           ("OccurredAt", "Action", "UserId", "UserName", "EntityType", "EntityId", "Changes", "Details", "IpAddress", "Request")
           VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)"#,
    )
    .bind(Ts::now())
    .bind(entry.action)
    .bind(entry.user_id)
    .bind(entry.user_name.map(|name| truncate_chars(name, MAX_USER_NAME_LENGTH)))
    .bind(entry.entity_type)
    .bind(entry.entity_id)
    .bind(sqlx::types::Json(entry.changes))
    .bind(entry.details)
    .bind(actor.ip_address.as_deref())
    .bind(actor.request.as_deref())
    .execute(conn)
    .await?;
    Ok(())
}

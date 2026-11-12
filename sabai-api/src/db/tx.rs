//! An audited database transaction.
//!
//! Every write that should appear in the audit log goes through [`Tx::insert`],
//! [`Tx::update`] or [`Tx::delete`]: the change and its audit entry are written in the
//! same transaction, attributed to the request's staff user. Raw `UPDATE`/`DELETE`
//! statements bypass the log, so load rows and change them through these methods instead.

use std::ops::{Deref, DerefMut};

use sqlx::{PgConnection, Postgres, QueryBuilder};
use uuid::Uuid;

use super::audit::{self, AuditAction, AuditActor, NewEntry};
use super::entity::{Entity, Kind};
use crate::time::Ts;

pub struct Tx {
    inner: sqlx::Transaction<'static, Postgres>,
    actor: AuditActor,
}

impl Deref for Tx {
    type Target = PgConnection;

    fn deref(&self) -> &PgConnection {
        &self.inner
    }
}

impl DerefMut for Tx {
    fn deref_mut(&mut self) -> &mut PgConnection {
        &mut self.inner
    }
}

impl Tx {
    pub async fn begin(pool: &super::Pool, actor: AuditActor) -> Result<Self, sqlx::Error> {
        Ok(Tx { inner: pool.begin().await?, actor })
    }

    pub fn actor(&self) -> &AuditActor {
        &self.actor
    }

    pub async fn commit(self) -> Result<(), sqlx::Error> {
        self.inner.commit().await
    }

    pub async fn rollback(self) -> Result<(), sqlx::Error> {
        self.inner.rollback().await
    }

    /// Inserts the row and returns it as stored (with its generated key).
    pub async fn insert<E: Entity>(&mut self, mut entity: E) -> Result<E, sqlx::Error> {
        entity.before_insert(Ts::now());
        let mut query = QueryBuilder::<Postgres>::new(format!("INSERT INTO \"{}\" ", E::TABLE));
        let columns: Vec<_> = E::FIELDS
            .iter()
            .zip(entity.values())
            .filter(|(field, _)| field.kind != Kind::PkAuto)
            .collect();
        if columns.is_empty() {
            query.push("DEFAULT VALUES");
        } else {
            query.push("(");
            for (index, (field, _)) in columns.iter().enumerate() {
                if index > 0 {
                    query.push(", ");
                }
                query.push(format!("\"{}\"", field.column));
            }
            query.push(") VALUES (");
            for (index, (_, value)) in columns.iter().enumerate() {
                if index > 0 {
                    query.push(", ");
                }
                value.bind(&mut query);
            }
            query.push(")");
        }
        query.push(" RETURNING *");
        let stored: E = query.build_query_as().fetch_one(&mut *self.inner).await?;

        let changes = audit::snapshot(&stored, true);
        self.log(AuditAction::Created, E::NAME, &audit::entity_id(&stored), &changes).await?;
        Ok(stored)
    }

    /// Writes the columns that differ between `before` and `after`, and logs them.
    /// Nothing is written when nothing changed.
    pub async fn update<E: Entity>(&mut self, before: &E, after: E) -> Result<E, sqlx::Error> {
        let before_values = before.values();
        let after_values = after.values();
        let changed: Vec<usize> = (0..E::FIELDS.len())
            .filter(|&index| !before_values[index].same(after_values[index]))
            .collect();
        if changed.is_empty() {
            return Ok(after);
        }

        let mut query = QueryBuilder::<Postgres>::new(format!("UPDATE \"{}\" SET ", E::TABLE));
        for (position, &index) in changed.iter().enumerate() {
            if position > 0 {
                query.push(", ");
            }
            query.push(format!("\"{}\" = ", E::FIELDS[index].column));
            after_values[index].bind(&mut query);
        }
        push_key_filter::<E>(&mut query, &before_values);
        query.build().execute(&mut *self.inner).await?;

        let changes = audit::diff(before, &after);
        if !changes.is_empty() {
            self.log(AuditAction::Updated, E::NAME, &audit::entity_id(&after), &changes).await?;
        }
        Ok(after)
    }

    pub async fn delete<E: Entity>(&mut self, entity: &E) -> Result<(), sqlx::Error> {
        let mut query = QueryBuilder::<Postgres>::new(format!("DELETE FROM \"{}\"", E::TABLE));
        push_key_filter::<E>(&mut query, &entity.values());
        query.build().execute(&mut *self.inner).await?;

        let changes = audit::snapshot(entity, false);
        self.log(AuditAction::Deleted, E::NAME, &audit::entity_id(entity), &changes).await
    }

    async fn log(
        &mut self,
        action: AuditAction,
        entity_type: &str,
        entity_id: &str,
        changes: &[audit::FieldChange],
    ) -> Result<(), sqlx::Error> {
        let entry = NewEntry {
            action,
            user_id: self.actor.user_id,
            user_name: self.actor.user_name.as_deref(),
            entity_type: Some(entity_type),
            entity_id: Some(entity_id),
            changes,
            details: None,
        };
        audit::write(&mut self.inner, &self.actor, entry).await
    }

    /// Records an event that is not a row change, such as a sign-in. `user` is the
    /// account it concerns; without one the signed-in user of the request is used, or
    /// `user_name` alone (an attempted name that matched no account).
    pub async fn record(
        &mut self,
        action: AuditAction,
        user: Option<(Uuid, Option<&str>)>,
        user_name: Option<&str>,
        details: Option<&str>,
    ) -> Result<(), sqlx::Error> {
        let name = user
            .and_then(|(_, name)| name)
            .or(user_name)
            .or(self.actor.user_name.as_deref())
            .map(str::to_owned);
        let entry = NewEntry {
            action,
            user_id: user.map(|(id, _)| id).or(self.actor.user_id),
            user_name: name.as_deref(),
            entity_type: None,
            entity_id: None,
            changes: &[],
            details,
        };
        audit::write(&mut self.inner, &self.actor, entry).await
    }
}

fn push_key_filter<E: Entity>(query: &mut QueryBuilder<'_, Postgres>, values: &[&dyn super::Value]) {
    let mut first = true;
    for (field, value) in E::FIELDS.iter().zip(values) {
        if !field.kind.is_key() {
            continue;
        }
        query.push(if first { " WHERE " } else { " AND " });
        query.push(format!("\"{}\" = ", field.column));
        value.bind(query);
        first = false;
    }
    assert!(!first, "entity {} has no key column", E::NAME);
}

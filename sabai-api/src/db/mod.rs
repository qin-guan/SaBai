//! Data access: the connection pool, entity mapping, audited transactions and migrations.

pub mod audit;
pub mod entity;
pub mod migrate;
pub mod tx;

pub use audit::{AuditAction, AuditActor};
pub use entity::{AuditFormat, Entity, Field, Kind, Value};
pub use tx::Tx;

use sqlx::postgres::PgPoolOptions;

pub type Pool = sqlx::PgPool;

pub async fn connect(settings: &crate::config::DatabaseSettings) -> anyhow::Result<Pool> {
    Ok(PgPoolOptions::new()
        .max_connections(settings.max_connections)
        .acquire_timeout(std::time::Duration::from_secs(30))
        .connect_with(settings.connect_options()?)
        .await?)
}

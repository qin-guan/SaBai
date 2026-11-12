//! Schema migrations.
//!
//! Migrations live in `migrations/` and run at startup. The first one is the schema the
//! .NET API's EF Core migrations produced. A database EF Core already migrated is adopted
//! rather than rebuilt: when its history is at the last EF migration, the baseline is
//! recorded as applied and only later migrations run.

use anyhow::{Context, bail};
use sqlx::migrate::{Migrate, Migrator};

use super::Pool;

static MIGRATOR: Migrator = sqlx::migrate!("./migrations");

/// The last EF Core migration; the baseline equals the schema at this point.
const LAST_EF_MIGRATION: &str = "20261109033008_GeneralizeFilesAndAddDentalImage";
const BASELINE_VERSION: i64 = 1;

pub async fn run(pool: &Pool) -> anyhow::Result<()> {
    adopt_ef_core_database(pool).await?;
    MIGRATOR.run(pool).await.context("Database migration failed.")?;
    Ok(())
}

async fn adopt_ef_core_database(pool: &Pool) -> anyhow::Result<()> {
    let has_ef_history: bool =
        sqlx::query_scalar(r#"SELECT to_regclass('public."__EFMigrationsHistory"') IS NOT NULL"#)
            .fetch_one(pool)
            .await?;
    if !has_ef_history {
        return Ok(());
    }

    let mut conn = pool.acquire().await?;
    conn.ensure_migrations_table().await?;
    let already_adopted = conn
        .list_applied_migrations()
        .await?
        .iter()
        .any(|applied| applied.version == BASELINE_VERSION);
    if already_adopted {
        return Ok(());
    }

    let last: Option<String> = sqlx::query_scalar(
        r#"SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1"#,
    )
    .fetch_optional(&mut *conn)
    .await?;
    if last.as_deref() != Some(LAST_EF_MIGRATION) {
        bail!(
            "This database was migrated by the .NET API up to '{}', but it can only be adopted at \
             '{LAST_EF_MIGRATION}'. Run the .NET API's migrations to that point first.",
            last.as_deref().unwrap_or("(nothing)")
        );
    }

    let baseline = MIGRATOR
        .iter()
        .find(|migration| migration.version == BASELINE_VERSION)
        .context("The baseline migration is missing.")?;
    sqlx::query(
        "INSERT INTO _sqlx_migrations (version, description, success, checksum, execution_time) \
         VALUES ($1, $2, TRUE, $3, 0)",
    )
    .bind(baseline.version)
    .bind(&*baseline.description)
    .bind(&*baseline.checksum)
    .execute(&mut *conn)
    .await?;
    tracing::info!("Adopted the existing EF Core schema as the migration baseline");
    Ok(())
}

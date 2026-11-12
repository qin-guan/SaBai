//! Startup seeding for roles, the optional bootstrap admin and demo staff.

use uuid::Uuid;

use super::users::{self, NewUser, Role, UserError, Users, roles};
use crate::config::{AuthSettings, IdentitySettings};
use crate::db::{AuditActor, Pool, Tx};

pub const DEMO_PASSWORD: &str = "Password1!";

/// (email, username, nickname, role)
pub const DEMO_PEOPLE: &[(&str, &str, &str, &str)] = &[
    ("admin@sabai.test", "admin", "Admin", roles::ADMIN),
    ("doctor@sabai.test", "doctor", "Doctor", roles::MEMBER),
    ("pharmacist@sabai.test", "pharmacist", "Pharmacist", roles::MEMBER),
    ("registration@sabai.test", "registration", "Registration", roles::MEMBER),
];

pub async fn ensure(pool: &Pool, auth: &AuthSettings, identity: &IdentitySettings) -> anyhow::Result<()> {
    let mut tx = Tx::begin(pool, AuditActor::system()).await?;

    for role in roles::ALL {
        let exists: bool =
            sqlx::query_scalar(r#"SELECT EXISTS (SELECT 1 FROM "AspNetRoles" WHERE "NormalizedName" = $1)"#)
                .bind(users::normalize(role))
                .fetch_one(&mut *tx)
                .await?;
        if !exists {
            tx.insert(Role {
                id: Uuid::new_v4(),
                name: Some((*role).to_owned()),
                normalized_name: Some(users::normalize(role)),
                concurrency_stamp: Some(Uuid::new_v4().to_string()),
            })
            .await?;
        }
    }

    let bootstrap = &auth.bootstrap_admin;
    if !bootstrap.user_name.trim().is_empty() && !bootstrap.password.trim().is_empty() {
        let name = bootstrap.user_name.trim();
        ensure_user(&mut tx, identity, name, bootstrap.email.as_deref(), name, roles::ADMIN, &bootstrap.password)
            .await?;
    }

    if auth.seed_demo_users {
        for (email, user_name, nickname, role) in DEMO_PEOPLE {
            ensure_user(&mut tx, identity, user_name, Some(email), nickname, role, DEMO_PASSWORD).await?;
        }
    }

    tx.commit().await?;
    Ok(())
}

async fn ensure_user(
    tx: &mut Tx,
    identity: &IdentitySettings,
    user_name: &str,
    email: Option<&str>,
    nickname: &str,
    role: &str,
    password: &str,
) -> anyhow::Result<()> {
    let describe = |what: String| {
        move |error: UserError| match error {
            UserError::Identity(errors) => anyhow::anyhow!(
                "Could not seed {what}: {}",
                errors.iter().map(|e| e.description.as_str()).collect::<Vec<_>>().join(" ")
            ),
            UserError::Api(error) => anyhow::anyhow!("Could not seed {what}: {error}"),
        }
    };

    let existing = users::find_by_name(tx, user_name).await?;
    let mut accounts = Users::new(tx, identity);
    let mut user = match existing {
        Some(mut user) => {
            if !user.has_password() {
                accounts
                    .add_password(&mut user, password)
                    .await
                    .map_err(describe(format!("password for {user_name}")))?;
                tracing::info!(user_name, "Set the configured password for the staff user");
            }
            user
        }
        None => {
            let new = NewUser {
                user_name: user_name.to_owned(),
                email: email.map(str::trim).filter(|email| !email.is_empty()).map(str::to_owned),
                nickname: nickname.to_owned(),
            };
            let user = accounts.create(new, password).await.map_err(describe(format!("user {user_name}")))?;
            tracing::info!(user_name, "Seeded staff user");
            user
        }
    };

    let current = users::roles_of(accounts.tx, user.id).await?;
    if !current.iter().any(|existing| existing == role) {
        accounts
            .add_to_roles(&mut user, &[role])
            .await
            .map_err(describe(format!("role {role} for {user_name}")))?;
    }
    Ok(())
}

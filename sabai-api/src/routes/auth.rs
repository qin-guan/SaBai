//! Sign-in, the signed-in user's own account, and staff administration.

use axum::Router;
use axum::http::HeaderMap;
use axum::routing::{get, post};
use serde::{Deserialize, Serialize};
use sqlx::PgConnection;
use uuid::Uuid;

use crate::auth::signin::{self, SignIn, SignInResult};
use crate::auth::users::{self, StaffUser};
use crate::auth::Staff;
use crate::db::{AuditAction, Tx};
use crate::error::{ApiError, ApiResult};
use crate::http::{Ctx, Json, NoContent, Req, Rules, SetCookies, Validate};
use crate::state::AppState;
use crate::time::Ts;

/// Staff profile returned by authentication and user endpoints.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct UserResponse {
    pub id: Uuid,
    pub user_name: String,
    pub email: Option<String>,
    pub nickname: String,
    pub roles: Vec<String>,
    /// True while the account cannot sign in.
    pub is_locked: bool,
    /// When the lock ends. Far in the future (year 9999) when an admin locked the
    /// account; a few minutes ahead after too many failed sign-ins.
    pub lockout_end: Option<Ts>,
    pub two_factor_enabled: bool,
    /// False for accounts that still need a password set by an admin.
    pub has_password: bool,
    pub created_at: Ts,
}

impl UserResponse {
    pub fn from(user: &StaffUser, roles: Vec<String>) -> Self {
        let locked = user.is_locked_out(Ts::now());
        UserResponse {
            id: user.id,
            user_name: user.name().to_owned(),
            email: user.email.clone(),
            nickname: user.nickname.clone(),
            roles,
            is_locked: locked,
            lockout_end: if locked { user.lockout_end } else { None },
            two_factor_enabled: user.two_factor_enabled,
            has_password: user.has_password(),
            created_at: user.created_at,
        }
    }

    pub async fn load(conn: &mut PgConnection, user: &StaffUser) -> Result<Self, sqlx::Error> {
        Ok(Self::from(user, users::roles_of(conn, user.id).await?))
    }
}

/// Result of a sign-in step. On success the session cookie is set and `user` is
/// returned. When the account has two-factor on, `requiresTwoFactor` is true and the
/// client must finish with `POST /auth/login/two-factor` (or a recovery code).
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SignInResponse {
    pub requires_two_factor: bool,
    pub user: Option<UserResponse>,
}

/// Turns a sign-in outcome into the response, recording it in the audit log. The
/// transaction is committed in every case: failed attempts count towards lockout.
pub async fn finish_sign_in(
    mut tx: Tx,
    outcome: SignIn,
    user: &StaffUser,
    method: &str,
) -> ApiResult<(SetCookies, Json<SignInResponse>)> {
    let account = Some((user.id, user.user_name.as_deref()));
    let failure = match outcome.result {
        SignInResult::Succeeded => {
            tx.record(AuditAction::SignedIn, account, None, Some(method)).await?;
            let body = SignInResponse { requires_two_factor: false, user: Some(UserResponse::load(&mut tx, user).await?) };
            tx.commit().await?;
            return Ok((SetCookies(outcome.cookies), Json(body)));
        }
        SignInResult::RequiresTwoFactor => {
            tx.commit().await?;
            return Ok((SetCookies(outcome.cookies), Json(SignInResponse { requires_two_factor: true, user: None })));
        }
        SignInResult::LockedOut => {
            tx.record(AuditAction::SignInFailed, account, None, Some(&format!("{method}: account locked"))).await?;
            ApiError::forbidden("This account is locked. Try again later or ask an admin to unlock it.")
        }
        SignInResult::Failed => {
            tx.record(AuditAction::SignInFailed, account, None, Some(&format!("{method}: rejected"))).await?;
            ApiError::AuthRejected
        }
    };
    tx.commit().await?;
    Err(failure)
}

/// Username (or email) and password.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct LoginRequest {
    /// Username, or the account's email address.
    pub user_name: String,
    pub password: String,
    /// Keep the session cookie after the browser closes.
    #[serde(default)]
    pub remember_me: bool,
}

impl Validate for LoginRequest {
    fn validate(&self, v: &mut Rules) {
        v.not_empty("userName", &self.user_name);
        v.not_empty("password", &self.password);
    }
}

/// Sign in with username and password.
///
/// Checks the password and sets the session cookie. Repeated failures lock the account
/// for a few minutes. When the account has two-factor on, no session is created yet:
/// `requiresTwoFactor` is true and the client continues with `POST /auth/login/two-factor`.
async fn login(
    ctx: Ctx,
    headers: HeaderMap,
    Req(req): Req<LoginRequest>,
) -> ApiResult<(SetCookies, Json<SignInResponse>)> {
    let name = req.user_name.trim();
    let mut tx = ctx.begin().await?;
    let mut user = users::find_by_name(&mut tx, name).await?;
    if user.is_none() && name.contains('@') {
        user = users::find_by_email(&mut tx, name).await?;
    }
    let Some(mut user) = user else {
        tx.record(AuditAction::SignInFailed, None, Some(name), Some("Password: unknown user")).await?;
        tx.commit().await?;
        return Err(ApiError::AuthRejected);
    };

    let outcome =
        signin::password_sign_in(&ctx.state, &mut tx, &headers, &mut user, &req.password, req.remember_me).await?;
    finish_sign_in(tx, outcome, &user, "Password").await
}

/// Sign out.
async fn logout(ctx: Ctx) -> ApiResult<(SetCookies, NoContent)> {
    if ctx.principal.is_some() {
        let mut tx = ctx.begin().await?;
        tx.record(AuditAction::SignedOut, None, None, None).await?;
        tx.commit().await?;
    }
    Ok((SetCookies(signin::sign_out()), NoContent))
}

/// The staff user represented by the current session cookie.
#[derive(Debug, Serialize)]
pub struct WhoAmIResponse {
    pub user: UserResponse,
}

/// Return the signed-in staff user.
async fn who_am_i(ctx: Ctx, _: Staff) -> ApiResult<Json<WhoAmIResponse>> {
    let mut conn = ctx.db().acquire().await?;
    let user = ctx.current_user(&mut conn).await?;
    Ok(Json(WhoAmIResponse { user: UserResponse::load(&mut conn, &user).await? }))
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/auth/login", post(login))
        .route("/auth/logout", post(logout))
        .route("/auth/whoami", get(who_am_i))
}

//! The signed-in user's own account: password and two-factor authentication.

use axum::Router;
use axum::http::HeaderMap;
use axum::routing::{get, post};
use serde::{Deserialize, Serialize};

use crate::auth::users::{self, Users};
use crate::auth::{Staff, session, signin};
use crate::error::{ApiError, ApiResult};
use crate::http::{Ctx, Json, NoContent, Req, Rules, SetCookies, Validate};
use crate::state::AppState;

const RECOVERY_CODE_COUNT: usize = 10;

/// A new authenticator secret, not active until confirmed with a code.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AuthenticatorResponse {
    /// The secret in groups of four, for typing into an app.
    pub shared_key: String,
    /// An `otpauth://` URI to show as a QR code.
    pub authenticator_uri: String,
}

/// `abcd efgh …`: the key in lowercase groups of four.
fn format_key(key: &str) -> String {
    let characters: Vec<char> = key.chars().collect();
    characters.chunks(4).map(|group| group.iter().collect::<String>()).collect::<Vec<_>>().join(" ").to_lowercase()
}

/// Percent-encodes everything but RFC 3986 unreserved characters.
fn escape_data_string(text: &str) -> String {
    let mut escaped = String::with_capacity(text.len());
    for byte in text.bytes() {
        if byte.is_ascii_alphanumeric() || matches!(byte, b'-' | b'.' | b'_' | b'~') {
            escaped.push(byte as char);
        } else {
            escaped.push_str(&format!("%{byte:02X}"));
        }
    }
    escaped
}

/// Start authenticator (TOTP) setup.
///
/// Generates a new authenticator secret. Two-factor stays off until the code from the
/// app is confirmed with `POST /auth/account/two-factor/enable`.
async fn authenticator(ctx: Ctx, _: Staff) -> ApiResult<(SetCookies, Json<AuthenticatorResponse>)> {
    let mut tx = ctx.begin().await?;
    let mut user = ctx.current_user(&mut tx).await?;
    if user.two_factor_enabled {
        return Err(ApiError::conflict(
            "Two-factor authentication is already on. Turn it off before setting up a new authenticator.",
        ));
    }

    let key = Users::new(&mut tx, &ctx.state.settings.identity).reset_authenticator_key(&mut user).await?;
    // Resetting the key rotates the security stamp; re-issue this session's cookie.
    let cookie = signin::refresh_sign_in(&ctx.state, &mut tx, &user, ctx.ticket.as_ref()).await?;
    tx.commit().await?;

    let issuer = escape_data_string(&ctx.state.settings.auth.authenticator_issuer);
    let account = escape_data_string(&user.user_name.clone().unwrap_or_else(|| user.id.to_string()));
    Ok((
        SetCookies(vec![cookie]),
        Json(AuthenticatorResponse {
            shared_key: format_key(&key),
            authenticator_uri: format!("otpauth://totp/{issuer}:{account}?secret={key}&issuer={issuer}&digits=6"),
        }),
    ))
}

/// Change the signed-in user's own password.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PasswordRequest {
    pub current_password: String,
    pub new_password: String,
}

impl Validate for PasswordRequest {
    fn validate(&self, v: &mut Rules) {
        v.not_empty("currentPassword", &self.current_password);
        v.not_empty("newPassword", &self.new_password);
    }
}

/// Change your password.
///
/// Signs out your other sessions; this one stays signed in.
async fn change_password(ctx: Ctx, _: Staff, Req(req): Req<PasswordRequest>) -> ApiResult<(SetCookies, NoContent)> {
    let mut tx = ctx.begin().await?;
    let mut user = ctx.current_user(&mut tx).await?;
    let changed = Users::new(&mut tx, &ctx.state.settings.identity)
        .change_password(&mut user, &req.current_password, &req.new_password)
        .await;
    match changed {
        Err(error) if error.has_code("PasswordMismatch") => {
            return Err(ApiError::bad_request("Current password is incorrect."));
        }
        other => other?,
    }
    let cookie = signin::refresh_sign_in(&ctx.state, &mut tx, &user, ctx.ticket.as_ref()).await?;
    tx.commit().await?;
    Ok((SetCookies(vec![cookie]), NoContent))
}

/// One-time recovery codes. Shown once; earlier codes stop working.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RecoveryCodesResponse {
    pub recovery_codes: Vec<String>,
}

/// Replace your recovery codes.
async fn recovery_codes(ctx: Ctx, _: Staff) -> ApiResult<Json<RecoveryCodesResponse>> {
    let mut tx = ctx.begin().await?;
    let mut user = ctx.current_user(&mut tx).await?;
    if !user.two_factor_enabled {
        return Err(ApiError::bad_request("Two-factor authentication is off."));
    }
    let recovery_codes = Users::new(&mut tx, &ctx.state.settings.identity)
        .generate_recovery_codes(&mut user, RECOVERY_CODE_COUNT)
        .await?;
    tx.commit().await?;
    Ok(Json(RecoveryCodesResponse { recovery_codes }))
}

/// Two-factor state of the signed-in user.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct TwoFactorResponse {
    pub is_enabled: bool,
    pub recovery_codes_left: usize,
    /// This browser skips the code at sign-in.
    pub is_machine_remembered: bool,
}

/// Get your two-factor status.
async fn two_factor(ctx: Ctx, _: Staff, headers: HeaderMap) -> ApiResult<Json<TwoFactorResponse>> {
    let mut conn = ctx.db().acquire().await?;
    let user = ctx.current_user(&mut conn).await?;
    Ok(Json(TwoFactorResponse {
        is_enabled: user.two_factor_enabled,
        recovery_codes_left: users::count_recovery_codes(&mut conn, user.id).await?,
        is_machine_remembered: session::is_two_factor_remembered(&ctx.state.keys, &headers, &user),
    }))
}

/// The current code from the authenticator app, proving it was set up.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct TwoFactorEnableRequest {
    pub code: String,
}

impl Validate for TwoFactorEnableRequest {
    fn validate(&self, v: &mut Rules) {
        v.not_empty("code", &self.code).max_len("code", &self.code, 16);
    }
}

/// Turn on two-factor authentication.
///
/// Confirms the authenticator from `POST /auth/account/authenticator` and returns ten
/// one-time recovery codes.
async fn two_factor_enable(
    ctx: Ctx,
    _: Staff,
    Req(req): Req<TwoFactorEnableRequest>,
) -> ApiResult<(SetCookies, Json<RecoveryCodesResponse>)> {
    let mut tx = ctx.begin().await?;
    let mut user = ctx.current_user(&mut tx).await?;
    if user.two_factor_enabled {
        return Err(ApiError::conflict("Two-factor authentication is already on."));
    }
    if users::authenticator_key(&mut tx, user.id).await?.is_none_or(|key| key.is_empty()) {
        return Err(ApiError::bad_request("Set up an authenticator first."));
    }

    let code = req.code.replace([' ', '-'], "");
    let mut accounts = Users::new(&mut tx, &ctx.state.settings.identity);
    if !accounts.verify_authenticator_code(&user, &code).await? {
        return Err(ApiError::bad_request("The verification code is not valid."));
    }
    accounts.set_two_factor_enabled(&mut user, true).await?;
    let recovery_codes = accounts.generate_recovery_codes(&mut user, RECOVERY_CODE_COUNT).await?;
    let cookie = signin::refresh_sign_in(&ctx.state, &mut tx, &user, ctx.ticket.as_ref()).await?;
    tx.commit().await?;
    Ok((SetCookies(vec![cookie]), Json(RecoveryCodesResponse { recovery_codes })))
}

/// Your password, to confirm turning two-factor off.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct TwoFactorDisableRequest {
    pub password: String,
}

impl Validate for TwoFactorDisableRequest {
    fn validate(&self, v: &mut Rules) {
        v.not_empty("password", &self.password);
    }
}

/// Turn off two-factor authentication.
async fn two_factor_disable(
    ctx: Ctx,
    _: Staff,
    Req(req): Req<TwoFactorDisableRequest>,
) -> ApiResult<(SetCookies, NoContent)> {
    let mut tx = ctx.begin().await?;
    let mut user = ctx.current_user(&mut tx).await?;
    if !users::check_password(&user, &req.password) {
        return Err(ApiError::bad_request("Password is incorrect."));
    }
    Users::new(&mut tx, &ctx.state.settings.identity).set_two_factor_enabled(&mut user, false).await?;
    let cookie = signin::refresh_sign_in(&ctx.state, &mut tx, &user, ctx.ticket.as_ref()).await?;
    tx.commit().await?;
    Ok((SetCookies(vec![signin::forget_two_factor_client(), cookie]), NoContent))
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/auth/account/authenticator", post(authenticator))
        .route("/auth/account/password", post(change_password))
        .route("/auth/account/recovery-codes", post(recovery_codes))
        .route("/auth/account/two-factor", get(two_factor))
        .route("/auth/account/two-factor/enable", post(two_factor_enable))
        .route("/auth/account/two-factor/disable", post(two_factor_disable))
}

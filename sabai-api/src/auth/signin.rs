//! Sign-in flows: password, then (when two-factor is on) an authenticator or recovery
//! code. Behaviour follows ASP.NET Core Identity's `SignInManager`, which this replaces:
//! failed attempts count towards a temporary lockout, a correct password resets the
//! count, and a locked account is refused before its password is even checked.
//!
//! Each function returns the outcome plus the `Set-Cookie` headers to send. The caller
//! commits the transaction either way: failed attempts must be saved too.

use axum::http::{HeaderMap, HeaderValue};
use sqlx::PgConnection;

use super::session::{self, Ticket};
use super::users::{self, StaffUser, UserResult, Users};
use crate::db::Tx;
use crate::state::AppState;
use crate::time::Ts;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SignInResult {
    Succeeded,
    /// The password was right; a second factor is needed to finish.
    RequiresTwoFactor,
    LockedOut,
    Failed,
}

#[derive(Debug)]
pub struct SignIn {
    pub result: SignInResult,
    pub cookies: Vec<HeaderValue>,
}

impl SignIn {
    fn only(result: SignInResult) -> Self {
        SignIn { result, cookies: Vec::new() }
    }
}

async fn session_cookie(
    state: &AppState,
    conn: &mut PgConnection,
    user: &StaffUser,
    persistent: bool,
) -> Result<HeaderValue, sqlx::Error> {
    let roles = users::roles_of(conn, user.id).await?;
    Ok(session::issue(&state.keys, &state.settings.auth, user, roles, persistent).1)
}

/// Checks the password and either signs in or asks for a second factor.
pub async fn password_sign_in(
    state: &AppState,
    tx: &mut Tx,
    headers: &HeaderMap,
    user: &mut StaffUser,
    password: &str,
    persistent: bool,
) -> UserResult<SignIn> {
    if user.is_locked_out(Ts::now()) {
        return Ok(SignIn::only(SignInResult::LockedOut));
    }

    let two_factor = users::is_two_factor_active(tx, user).await?;
    let remembered = session::is_two_factor_remembered(&state.keys, headers, user);
    if !users::check_password(user, password) {
        let mut accounts = Users::new(tx, &state.settings.identity);
        accounts.access_failed(user).await?;
        let result = if user.is_locked_out(Ts::now()) { SignInResult::LockedOut } else { SignInResult::Failed };
        return Ok(SignIn::only(result));
    }

    // With two-factor pending, the failure count is only cleared once the code is right.
    if !two_factor || remembered {
        Users::new(tx, &state.settings.identity).reset_access_failed_count(user).await?;
    }
    if two_factor && !remembered {
        return Ok(SignIn {
            result: SignInResult::RequiresTwoFactor,
            cookies: vec![session::two_factor_user_cookie(&state.keys, user.id)],
        });
    }
    let cookie = session_cookie(state, tx, user, persistent).await?;
    Ok(SignIn { result: SignInResult::Succeeded, cookies: vec![cookie] })
}

/// The user who passed the password step and now owes a second factor.
pub async fn two_factor_user(
    state: &AppState,
    conn: &mut PgConnection,
    headers: &HeaderMap,
) -> Result<Option<StaffUser>, sqlx::Error> {
    match session::two_factor_user(&state.keys, headers) {
        Some(id) => users::find_by_id(conn, id).await,
        None => Ok(None),
    }
}

async fn finish_two_factor(
    state: &AppState,
    tx: &mut Tx,
    user: &mut StaffUser,
    persistent: bool,
    remember_client: bool,
) -> UserResult<SignIn> {
    Users::new(tx, &state.settings.identity).reset_access_failed_count(user).await?;
    let mut cookies = vec![session::delete_cookie(session::TWO_FACTOR_USER_COOKIE)];
    if remember_client {
        cookies.push(session::remember_two_factor_cookie(&state.keys, user));
    }
    cookies.push(session_cookie(state, tx, user, persistent).await?);
    Ok(SignIn { result: SignInResult::Succeeded, cookies })
}

/// Finishes sign-in with an authenticator code. A wrong code counts as a failed attempt.
pub async fn two_factor_authenticator_sign_in(
    state: &AppState,
    tx: &mut Tx,
    user: &mut StaffUser,
    code: &str,
    persistent: bool,
    remember_client: bool,
) -> UserResult<SignIn> {
    if user.is_locked_out(Ts::now()) {
        return Ok(SignIn::only(SignInResult::LockedOut));
    }
    let mut accounts = Users::new(tx, &state.settings.identity);
    if accounts.verify_authenticator_code(user, code).await? {
        return finish_two_factor(state, tx, user, persistent, remember_client).await;
    }
    accounts.access_failed(user).await?;
    let result = if user.is_locked_out(Ts::now()) { SignInResult::LockedOut } else { SignInResult::Failed };
    Ok(SignIn::only(result))
}

/// Finishes sign-in with a one-time recovery code. Wrong codes are not counted: the
/// codes are random, so guessing is not a practical attack.
pub async fn two_factor_recovery_code_sign_in(
    state: &AppState,
    tx: &mut Tx,
    user: &mut StaffUser,
    code: &str,
) -> UserResult<SignIn> {
    let redeemed = Users::new(tx, &state.settings.identity).redeem_recovery_code(user, code).await?;
    if redeemed {
        finish_two_factor(state, tx, user, false, false).await
    } else {
        Ok(SignIn::only(SignInResult::Failed))
    }
}

/// Re-issues the current session's cookie after the user's security stamp changed, so
/// the user stays signed in on this browser.
pub async fn refresh_sign_in(
    state: &AppState,
    conn: &mut PgConnection,
    user: &StaffUser,
    current: Option<&Ticket>,
) -> Result<HeaderValue, sqlx::Error> {
    session_cookie(state, conn, user, current.is_some_and(|ticket| ticket.persistent)).await
}

/// Ends the session (and any half-finished two-factor sign-in).
pub fn sign_out() -> Vec<HeaderValue> {
    vec![
        session::delete_cookie(session::SESSION_COOKIE),
        session::delete_cookie(session::TWO_FACTOR_USER_COOKIE),
    ]
}

/// Makes this browser be asked for a code again.
pub fn forget_two_factor_client() -> HeaderValue {
    session::delete_cookie(session::TWO_FACTOR_REMEMBER_COOKIE)
}

//! Staff authentication: accounts, passwords, two-factor and cookie sessions.
//!
//! - [`users`]: the account store and its rules (what `UserManager` did).
//! - [`signin`]: the sign-in flows (what `SignInManager` did).
//! - [`session`]: the session cookie.
//! - [`Staff`] / [`Admin`]: handler arguments that require a signed-in role.

pub mod password;
pub mod seed;
pub mod session;
pub mod signin;
pub mod totp;
pub mod users;

use axum::extract::FromRequestParts;
use axum::http::request::Parts;

pub use session::Principal;
pub use users::roles;

use crate::error::ApiError;
use crate::http::context::RequestContext;

fn principal(parts: &Parts) -> Result<Principal, ApiError> {
    parts
        .extensions
        .get::<RequestContext>()
        .and_then(|context| context.principal.clone())
        .ok_or(ApiError::Unauthenticated)
}

/// A signed-in staff user with the `admin` or `member` role. Put it first in a handler's
/// arguments so the session is checked before the request is read.
#[derive(Debug, Clone)]
pub struct Staff(pub Principal);

impl<S: Send + Sync> FromRequestParts<S> for Staff {
    type Rejection = ApiError;

    async fn from_request_parts(parts: &mut Parts, _state: &S) -> Result<Self, ApiError> {
        let principal = principal(parts)?;
        if principal.is_in_role(roles::ADMIN) || principal.is_in_role(roles::MEMBER) {
            Ok(Staff(principal))
        } else {
            Err(ApiError::AccessDenied)
        }
    }
}

/// A signed-in staff user with the `admin` role.
#[derive(Debug, Clone)]
pub struct Admin(pub Principal);

impl<S: Send + Sync> FromRequestParts<S> for Admin {
    type Rejection = ApiError;

    async fn from_request_parts(parts: &mut Parts, _state: &S) -> Result<Self, ApiError> {
        let principal = principal(parts)?;
        if principal.is_in_role(roles::ADMIN) { Ok(Admin(principal)) } else { Err(ApiError::AccessDenied) }
    }
}

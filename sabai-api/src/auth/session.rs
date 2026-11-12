//! Cookie sessions.
//!
//! The session is an encrypted, HttpOnly, SameSite=Lax cookie (`sabai.auth`), as before:
//! the web apps reach the API through a same-origin proxy, so no token handling in JS.
//! The cookie carries the user's id, name and roles, so most requests need no database
//! read; it is re-checked against the user's security stamp every
//! `Auth:SecurityStampValidationMinutes`, which is how role changes, locks and password
//! resets reach sessions that are already signed in. Expiry slides with activity.
//!
//! The encryption key lives in Postgres (`DataProtectionKeys`), so sessions survive
//! restarts and are shared by every API instance. Cookies issued by the .NET API cannot
//! be read (different format); those users sign in again once.

use std::time::Duration;

use axum::http::{HeaderMap, HeaderValue, header};
use base64::Engine;
use base64::engine::general_purpose::{STANDARD, URL_SAFE_NO_PAD};
use chacha20poly1305::aead::{Aead, KeyInit, Payload};
use chacha20poly1305::{XChaCha20Poly1305, XNonce};
use rand::RngCore;
use serde::de::DeserializeOwned;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::users::{self, StaffUser};
use crate::config::AuthSettings;
use crate::db::Pool;
use crate::time::Ts;

/// Name of the HttpOnly session cookie.
pub const SESSION_COOKIE: &str = "sabai.auth";
/// Holds the user between the password step and the second factor (five minutes).
pub const TWO_FACTOR_USER_COOKIE: &str = "Identity.TwoFactorUserId";
/// Marks a browser that asked not to be prompted for a code again.
pub const TWO_FACTOR_REMEMBER_COOKIE: &str = "Identity.TwoFactorRememberMe";

const KEY_NAME: &str = "sabai-api-session-key-v1";
const TWO_FACTOR_USER_LIFETIME: Duration = Duration::from_secs(5 * 60);
const TWO_FACTOR_REMEMBER_LIFETIME: Duration = Duration::from_secs(14 * 24 * 3600);

/// The signed-in staff user, as carried by the session cookie.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Principal {
    pub id: Uuid,
    pub user_name: String,
    pub email: Option<String>,
    pub roles: Vec<String>,
}

impl Principal {
    pub fn is_in_role(&self, role: &str) -> bool {
        self.roles.iter().any(|candidate| candidate == role)
    }

    /// Email when the account has one, otherwise the username.
    pub fn email_or_name(&self) -> &str {
        self.email.as_deref().filter(|email| !email.is_empty()).unwrap_or(&self.user_name)
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Ticket {
    pub uid: Uuid,
    pub name: String,
    pub email: Option<String>,
    pub roles: Vec<String>,
    /// Security stamp of the user when the ticket was last validated.
    pub stamp: String,
    /// Unix seconds.
    pub issued: i64,
    pub expires: i64,
    /// Keep the cookie after the browser closes ("remember me").
    pub persistent: bool,
}

impl Ticket {
    pub fn principal(&self) -> Principal {
        Principal {
            id: self.uid,
            user_name: self.name.clone(),
            email: self.email.clone(),
            roles: self.roles.clone(),
        }
    }
}

#[derive(Debug, Serialize, Deserialize)]
struct TwoFactorTicket {
    uid: Uuid,
    stamp: Option<String>,
    expires: i64,
}

/// Encrypts and decrypts cookie values. One key, kept in the database.
#[derive(Clone)]
pub struct SessionKeys {
    cipher: XChaCha20Poly1305,
}

impl SessionKeys {
    /// Loads the key, creating it on first start.
    pub async fn load(pool: &Pool) -> anyhow::Result<Self> {
        let mut candidate = [0u8; 32];
        rand::rng().fill_bytes(&mut candidate);
        let xml = format!("<sabaiSessionKey version=\"1\">{}</sabaiSessionKey>", STANDARD.encode(candidate));
        // Concurrent first starts may both insert; everyone then reads the oldest row.
        sqlx::query(
            r#"INSERT INTO "DataProtectionKeys" ("FriendlyName", "Xml")
               SELECT $1, $2 WHERE NOT EXISTS (SELECT 1 FROM "DataProtectionKeys" WHERE "FriendlyName" = $1)"#,
        )
        .bind(KEY_NAME)
        .bind(&xml)
        .execute(pool)
        .await?;
        let stored: String = sqlx::query_scalar(
            r#"SELECT "Xml" FROM "DataProtectionKeys" WHERE "FriendlyName" = $1 ORDER BY "Id" LIMIT 1"#,
        )
        .bind(KEY_NAME)
        .fetch_one(pool)
        .await?;
        let encoded = stored
            .split_once('>')
            .and_then(|(_, rest)| rest.split_once('<'))
            .map(|(encoded, _)| encoded)
            .ok_or_else(|| anyhow::anyhow!("The stored session key is malformed."))?;
        let key = STANDARD.decode(encoded.trim())?;
        Self::from_bytes(&key)
    }

    pub fn from_bytes(key: &[u8]) -> anyhow::Result<Self> {
        let cipher = XChaCha20Poly1305::new_from_slice(key)
            .map_err(|_| anyhow::anyhow!("The session key must be 32 bytes."))?;
        Ok(SessionKeys { cipher })
    }

    /// Encrypts `value` for the cookie `purpose`; a value sealed for one cookie cannot be
    /// replayed as another.
    fn seal<T: Serialize>(&self, purpose: &str, value: &T) -> String {
        let mut nonce = [0u8; 24];
        rand::rng().fill_bytes(&mut nonce);
        let plaintext = serde_json::to_vec(value).expect("tickets serialize");
        let sealed = self
            .cipher
            .encrypt(XNonce::from_slice(&nonce), Payload { msg: &plaintext, aad: purpose.as_bytes() })
            .expect("encryption does not fail");
        let mut out = nonce.to_vec();
        out.extend_from_slice(&sealed);
        URL_SAFE_NO_PAD.encode(out)
    }

    fn open<T: DeserializeOwned>(&self, purpose: &str, cookie: &str) -> Option<T> {
        let bytes = URL_SAFE_NO_PAD.decode(cookie).ok()?;
        if bytes.len() < 24 {
            return None;
        }
        let (nonce, sealed) = bytes.split_at(24);
        let plaintext = self
            .cipher
            .decrypt(XNonce::from_slice(nonce), Payload { msg: sealed, aad: purpose.as_bytes() })
            .ok()?;
        serde_json::from_slice(&plaintext).ok()
    }
}

pub fn read_cookie<'a>(headers: &'a HeaderMap, name: &str) -> Option<&'a str> {
    headers
        .get_all(header::COOKIE)
        .iter()
        .filter_map(|value| value.to_str().ok())
        .flat_map(|value| value.split(';'))
        .filter_map(|pair| pair.trim().split_once('='))
        .find(|(candidate, _)| *candidate == name)
        .map(|(_, value)| value)
}

fn set_cookie(name: &str, value: &str, expires: Option<Ts>) -> HeaderValue {
    let mut cookie = format!("{name}={value}");
    if let Some(expires) = expires {
        cookie.push_str(&format!("; expires={}", expires.0.format("%a, %d %b %Y %H:%M:%S GMT")));
    }
    cookie.push_str("; path=/; samesite=lax; httponly");
    HeaderValue::from_str(&cookie).expect("cookie values are ASCII")
}

/// A `Set-Cookie` that removes the cookie.
pub fn delete_cookie(name: &str) -> HeaderValue {
    HeaderValue::from_str(&format!(
        "{name}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; samesite=lax; httponly"
    ))
    .expect("cookie names are ASCII")
}

fn seconds(duration: Duration) -> i64 {
    duration.as_secs() as i64
}

/// A new session for `user`: the ticket and its `Set-Cookie` header.
pub fn issue(
    keys: &SessionKeys,
    settings: &AuthSettings,
    user: &StaffUser,
    roles: Vec<String>,
    persistent: bool,
) -> (Ticket, HeaderValue) {
    let now = Ts::now().unix_seconds();
    let ticket = Ticket {
        uid: user.id,
        name: user.name().to_owned(),
        email: user.email.clone(),
        roles,
        stamp: user.security_stamp.clone().unwrap_or_default(),
        issued: now,
        expires: now + seconds(settings.session_lifetime),
        persistent,
    };
    let cookie = session_cookie(keys, &ticket);
    (ticket, cookie)
}

fn session_cookie(keys: &SessionKeys, ticket: &Ticket) -> HeaderValue {
    let expires = ticket.persistent.then(|| Ts::from_unix_seconds(ticket.expires));
    set_cookie(SESSION_COOKIE, &keys.seal(SESSION_COOKIE, ticket), expires)
}

/// What the session cookie of a request turned out to be.
#[derive(Debug, Default)]
pub struct Authentication {
    pub ticket: Option<Ticket>,
    /// A refreshed (or removed) cookie to send back, unless the handler sets its own.
    pub set_cookie: Option<HeaderValue>,
}

/// Reads the session cookie: checks expiry, re-validates against the database when due,
/// and slides the expiry once more than half the lifetime has passed.
pub async fn authenticate(
    keys: &SessionKeys,
    settings: &AuthSettings,
    pool: &Pool,
    headers: &HeaderMap,
) -> Result<Authentication, sqlx::Error> {
    let Some(mut ticket) = read_cookie(headers, SESSION_COOKIE).and_then(|c| keys.open::<Ticket>(SESSION_COOKIE, c))
    else {
        return Ok(Authentication::default());
    };
    let now = Ts::now().unix_seconds();
    if ticket.expires <= now {
        return Ok(Authentication::default());
    }

    let mut renew = false;
    if now - ticket.issued >= seconds(settings.security_stamp_validation) {
        let mut conn = pool.acquire().await?;
        let user = users::find_by_id(&mut conn, ticket.uid).await?;
        let Some(user) = user.filter(|user| user.security_stamp.as_deref() == Some(ticket.stamp.as_str())) else {
            // The account is gone, or its stamp moved on (lock, password reset): sign out.
            return Ok(Authentication { ticket: None, set_cookie: Some(delete_cookie(SESSION_COOKIE)) });
        };
        ticket.name = user.name().to_owned();
        ticket.email = user.email.clone();
        ticket.roles = users::roles_of(&mut conn, user.id).await?;
        renew = true;
    } else if now - ticket.issued > ticket.expires - now {
        renew = true;
    }

    let set_cookie = renew.then(|| {
        ticket.expires = now + (ticket.expires - ticket.issued);
        ticket.issued = now;
        session_cookie(keys, &ticket)
    });
    Ok(Authentication { ticket: Some(ticket), set_cookie })
}

/// The cookie that carries the user from the password step to the second factor.
pub fn two_factor_user_cookie(keys: &SessionKeys, user_id: Uuid) -> HeaderValue {
    let ticket = TwoFactorTicket {
        uid: user_id,
        stamp: None,
        expires: Ts::now().unix_seconds() + seconds(TWO_FACTOR_USER_LIFETIME),
    };
    set_cookie(TWO_FACTOR_USER_COOKIE, &keys.seal(TWO_FACTOR_USER_COOKIE, &ticket), None)
}

/// The user waiting for a second factor, if the password step was passed recently.
pub fn two_factor_user(keys: &SessionKeys, headers: &HeaderMap) -> Option<Uuid> {
    let ticket: TwoFactorTicket =
        keys.open(TWO_FACTOR_USER_COOKIE, read_cookie(headers, TWO_FACTOR_USER_COOKIE)?)?;
    (ticket.expires > Ts::now().unix_seconds()).then_some(ticket.uid)
}

/// Remembers this browser so it is not asked for a code again (until the user's
/// security stamp changes).
pub fn remember_two_factor_cookie(keys: &SessionKeys, user: &StaffUser) -> HeaderValue {
    let expires = Ts::now().unix_seconds() + seconds(TWO_FACTOR_REMEMBER_LIFETIME);
    let ticket = TwoFactorTicket { uid: user.id, stamp: user.security_stamp.clone(), expires };
    set_cookie(
        TWO_FACTOR_REMEMBER_COOKIE,
        &keys.seal(TWO_FACTOR_REMEMBER_COOKIE, &ticket),
        Some(Ts::from_unix_seconds(expires)),
    )
}

pub fn is_two_factor_remembered(keys: &SessionKeys, headers: &HeaderMap, user: &StaffUser) -> bool {
    read_cookie(headers, TWO_FACTOR_REMEMBER_COOKIE)
        .and_then(|cookie| keys.open::<TwoFactorTicket>(TWO_FACTOR_REMEMBER_COOKIE, cookie))
        .is_some_and(|ticket| {
            ticket.uid == user.id
                && ticket.expires > Ts::now().unix_seconds()
                && ticket.stamp.as_deref() == user.security_stamp.as_deref()
        })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn sealed_values_open_only_for_their_purpose() {
        let keys = SessionKeys::from_bytes(&[7u8; 32]).unwrap();
        let sealed = keys.seal("a", &"hello");
        assert_eq!(keys.open::<String>("a", &sealed).as_deref(), Some("hello"));
        assert_eq!(keys.open::<String>("b", &sealed), None);
        assert_eq!(keys.open::<String>("a", "garbage"), None);
        let other = SessionKeys::from_bytes(&[8u8; 32]).unwrap();
        assert_eq!(other.open::<String>("a", &sealed), None);
    }

    #[test]
    fn reads_cookies_from_headers() {
        let mut headers = HeaderMap::new();
        headers.insert(header::COOKIE, HeaderValue::from_static("a=1; sabai.auth=xyz=; b=2"));
        assert_eq!(read_cookie(&headers, SESSION_COOKIE), Some("xyz="));
        assert_eq!(read_cookie(&headers, "missing"), None);
    }
}

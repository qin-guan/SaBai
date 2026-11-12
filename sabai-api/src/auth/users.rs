//! Staff accounts: the user store and the account rules.
//!
//! This is the part ASP.NET Core Identity's `UserManager` played. Tables, column
//! meanings, error codes and messages are kept, so existing accounts, roles, authenticator
//! keys and recovery codes carry over. Every method that changes a user saves it (and
//! writes its audit entry) before returning, like `UserManager` did.

use std::collections::HashMap;

use rand::Rng;
use sqlx::PgConnection;
use uuid::Uuid;

use super::{password, totp};
use crate::config::IdentitySettings;
use crate::db::Tx;
use crate::entity;
use crate::error::ApiError;
use crate::time::Ts;

entity! {
    /// Staff account. Profile fields plus the credential, lockout and two-factor state.
    pub struct StaffUser in "StaffUsers" {
        id: Uuid = "Id" [Pk],
        user_name: Option<String> = "UserName",
        normalized_user_name: Option<String> = "NormalizedUserName",
        email: Option<String> = "Email",
        normalized_email: Option<String> = "NormalizedEmail",
        email_confirmed: bool = "EmailConfirmed",
        password_hash: Option<String> = "PasswordHash" [Secret],
        security_stamp: Option<String> = "SecurityStamp" [Secret],
        concurrency_stamp: Option<String> = "ConcurrencyStamp" [Unaudited],
        phone_number: Option<String> = "PhoneNumber",
        phone_number_confirmed: bool = "PhoneNumberConfirmed",
        two_factor_enabled: bool = "TwoFactorEnabled",
        lockout_end: Option<Ts> = "LockoutEnd",
        lockout_enabled: bool = "LockoutEnabled",
        access_failed_count: i32 = "AccessFailedCount",
        nickname: String = "Nickname",
        created_at: Ts = "CreatedAt",
    }
}

impl StaffUser {
    /// True while the account cannot sign in, whether an admin locked it or too many
    /// failed sign-ins locked it temporarily.
    pub fn is_locked_out(&self, now: Ts) -> bool {
        self.lockout_enabled && self.lockout_end.is_some_and(|end| end > now)
    }

    pub fn has_password(&self) -> bool {
        self.password_hash.is_some()
    }

    pub fn name(&self) -> &str {
        self.user_name.as_deref().unwrap_or("")
    }
}

entity! {
    pub struct Role in "AspNetRoles" {
        id: Uuid = "Id" [Pk],
        name: Option<String> = "Name",
        normalized_name: Option<String> = "NormalizedName",
        concurrency_stamp: Option<String> = "ConcurrencyStamp" [Unaudited],
    }
}

entity! {
    pub struct UserRole in "AspNetUserRoles" {
        user_id: Uuid = "UserId" [PkFk],
        role_id: Uuid = "RoleId" [PkFk],
    }
}

entity! {
    /// Authenticator keys and recovery codes. Values are never written to the audit log.
    pub struct UserToken in "AspNetUserTokens" {
        user_id: Uuid = "UserId" [PkFk],
        login_provider: String = "LoginProvider" [Pk],
        name: String = "Name" [Pk],
        value: Option<String> = "Value" [Secret],
    }
}

/// Role names the API knows how to authorize. Seeded at startup.
pub mod roles {
    pub const ADMIN: &str = "admin";
    pub const MEMBER: &str = "member";
    pub const ALL: &[&str] = &[ADMIN, MEMBER];

    pub fn is_known(role: &str) -> bool {
        ALL.contains(&role)
    }
}

const TOKEN_PROVIDER: &str = "[AspNetUserStore]";
const AUTHENTICATOR_KEY: &str = "AuthenticatorKey";
const RECOVERY_CODES: &str = "RecoveryCodes";
const RECOVERY_CODE_CHARS: &[u8] = b"23456789BCDFGHJKMNPQRTVWXY";

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct IdentityError {
    pub code: &'static str,
    pub description: String,
}

impl IdentityError {
    fn new(code: &'static str, description: impl Into<String>) -> Self {
        IdentityError { code, description: description.into() }
    }
}

/// A rule the account data broke (`Identity`), or anything else that went wrong.
#[derive(Debug)]
pub enum UserError {
    Identity(Vec<IdentityError>),
    Api(ApiError),
}

pub type UserResult<T> = Result<T, UserError>;

impl UserError {
    pub fn has_code(&self, code: &str) -> bool {
        matches!(self, UserError::Identity(errors) if errors.iter().any(|error| error.code == code))
    }
}

impl From<sqlx::Error> for UserError {
    fn from(error: sqlx::Error) -> Self {
        UserError::Api(error.into())
    }
}

impl From<ApiError> for UserError {
    fn from(error: ApiError) -> Self {
        UserError::Api(error)
    }
}

/// Duplicates are 409, everything else (password rules, bad usernames…) is 400. Never
/// 401, so a failed self-service call does not log the web app out.
impl From<UserError> for ApiError {
    fn from(error: UserError) -> Self {
        match error {
            UserError::Api(error) => error,
            UserError::Identity(errors) => {
                let message =
                    errors.iter().map(|error| error.description.as_str()).collect::<Vec<_>>().join(" ");
                if errors.iter().any(|e| matches!(e.code, "DuplicateUserName" | "DuplicateEmail")) {
                    ApiError::Conflict(message)
                } else {
                    ApiError::BadRequest(message)
                }
            }
        }
    }
}

fn fail<T>(code: &'static str, description: impl Into<String>) -> UserResult<T> {
    Err(UserError::Identity(vec![IdentityError::new(code, description)]))
}

pub fn normalize(value: &str) -> String {
    value.to_uppercase()
}

pub struct NewUser {
    pub user_name: String,
    pub email: Option<String>,
    pub nickname: String,
}

pub async fn find_by_id(conn: &mut PgConnection, id: Uuid) -> Result<Option<StaffUser>, sqlx::Error> {
    sqlx::query_as(r#"SELECT * FROM "StaffUsers" WHERE "Id" = $1"#).bind(id).fetch_optional(conn).await
}

pub async fn find_by_name(conn: &mut PgConnection, name: &str) -> Result<Option<StaffUser>, sqlx::Error> {
    sqlx::query_as(r#"SELECT * FROM "StaffUsers" WHERE "NormalizedUserName" = $1 LIMIT 1"#)
        .bind(normalize(name))
        .fetch_optional(conn)
        .await
}

pub async fn find_by_email(conn: &mut PgConnection, email: &str) -> Result<Option<StaffUser>, sqlx::Error> {
    sqlx::query_as(r#"SELECT * FROM "StaffUsers" WHERE "NormalizedEmail" = $1 LIMIT 1"#)
        .bind(normalize(email))
        .fetch_optional(conn)
        .await
}

/// Role names of one user, in ordinal order.
pub async fn roles_of(conn: &mut PgConnection, user_id: Uuid) -> Result<Vec<String>, sqlx::Error> {
    let mut names: Vec<String> = sqlx::query_scalar(
        r#"SELECT r."Name" FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
           WHERE ur."UserId" = $1 AND r."Name" IS NOT NULL"#,
    )
    .bind(user_id)
    .fetch_all(conn)
    .await?;
    names.sort_by(|a, b| a.as_bytes().cmp(b.as_bytes()));
    Ok(names)
}

/// Role names for many users in one query, each list in ordinal order.
pub async fn roles_by_user(
    conn: &mut PgConnection,
    user_ids: &[Uuid],
) -> Result<HashMap<Uuid, Vec<String>>, sqlx::Error> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        r#"SELECT ur."UserId", r."Name" FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
           WHERE ur."UserId" = ANY($1) AND r."Name" IS NOT NULL"#,
    )
    .bind(user_ids)
    .fetch_all(conn)
    .await?;
    let mut map: HashMap<Uuid, Vec<String>> = HashMap::new();
    for (user_id, name) in rows {
        map.entry(user_id).or_default().push(name);
    }
    for names in map.values_mut() {
        names.sort_by(|a, b| a.as_bytes().cmp(b.as_bytes()));
    }
    Ok(map)
}

async fn token(conn: &mut PgConnection, user_id: Uuid, name: &str) -> Result<Option<UserToken>, sqlx::Error> {
    sqlx::query_as(
        r#"SELECT * FROM "AspNetUserTokens" WHERE "UserId" = $1 AND "LoginProvider" = $2 AND "Name" = $3"#,
    )
    .bind(user_id)
    .bind(TOKEN_PROVIDER)
    .bind(name)
    .fetch_optional(conn)
    .await
}

/// The authenticator secret, if one was set up.
pub async fn authenticator_key(conn: &mut PgConnection, user_id: Uuid) -> Result<Option<String>, sqlx::Error> {
    Ok(token(conn, user_id, AUTHENTICATOR_KEY).await?.and_then(|token| token.value))
}

pub async fn count_recovery_codes(conn: &mut PgConnection, user_id: Uuid) -> Result<usize, sqlx::Error> {
    let codes = token(conn, user_id, RECOVERY_CODES).await?.and_then(|token| token.value).unwrap_or_default();
    Ok(if codes.is_empty() { 0 } else { codes.split(';').count() })
}

/// True when a second factor must be asked for at sign-in: two-factor is on and some way
/// to produce a code exists (an authenticator key, or a confirmed email or phone).
pub async fn is_two_factor_active(conn: &mut PgConnection, user: &StaffUser) -> Result<bool, sqlx::Error> {
    if !user.two_factor_enabled {
        return Ok(false);
    }
    let has_email = user.email.as_deref().is_some_and(|email| !email.trim().is_empty()) && user.email_confirmed;
    let has_phone =
        user.phone_number.as_deref().is_some_and(|phone| !phone.trim().is_empty()) && user.phone_number_confirmed;
    if has_email || has_phone {
        return Ok(true);
    }
    Ok(authenticator_key(conn, user.id).await?.is_some_and(|key| !key.is_empty()))
}

pub fn check_password(user: &StaffUser, candidate: &str) -> bool {
    user.password_hash.as_deref().is_some_and(|hash| password::verify(hash, candidate))
}

/// Account operations inside one audited transaction.
pub struct Users<'a> {
    pub tx: &'a mut Tx,
    pub rules: &'a IdentitySettings,
}

impl<'a> Users<'a> {
    pub fn new(tx: &'a mut Tx, rules: &'a IdentitySettings) -> Self {
        Users { tx, rules }
    }

    fn validate_password(&self, candidate: &str) -> UserResult<()> {
        let rules = self.rules;
        let mut errors = Vec::new();
        if candidate.trim().is_empty() || candidate.encode_utf16().count() < rules.password_required_length {
            errors.push(IdentityError::new(
                "PasswordTooShort",
                format!("Passwords must be at least {} characters.", rules.password_required_length),
            ));
        }
        if rules.password_require_non_alphanumeric && candidate.chars().all(|c| c.is_ascii_alphanumeric()) {
            errors.push(IdentityError::new(
                "PasswordRequiresNonAlphanumeric",
                "Passwords must have at least one non alphanumeric character.",
            ));
        }
        if rules.password_require_digit && !candidate.chars().any(|c| c.is_ascii_digit()) {
            errors.push(IdentityError::new(
                "PasswordRequiresDigit",
                "Passwords must have at least one digit ('0'-'9').",
            ));
        }
        if rules.password_require_lowercase && !candidate.chars().any(|c| c.is_ascii_lowercase()) {
            errors.push(IdentityError::new(
                "PasswordRequiresLower",
                "Passwords must have at least one lowercase ('a'-'z').",
            ));
        }
        if rules.password_require_uppercase && !candidate.chars().any(|c| c.is_ascii_uppercase()) {
            errors.push(IdentityError::new(
                "PasswordRequiresUpper",
                "Passwords must have at least one uppercase ('A'-'Z').",
            ));
        }
        if rules.password_required_unique_chars >= 1 {
            let mut unique: Vec<char> = candidate.chars().collect();
            unique.sort_unstable();
            unique.dedup();
            if unique.len() < rules.password_required_unique_chars {
                errors.push(IdentityError::new(
                    "PasswordRequiresUniqueChars",
                    format!(
                        "Passwords must use at least {} different characters.",
                        rules.password_required_unique_chars
                    ),
                ));
            }
        }
        if errors.is_empty() { Ok(()) } else { Err(UserError::Identity(errors)) }
    }

    async fn validate_user(&mut self, user: &StaffUser) -> UserResult<()> {
        let name = user.name();
        let allowed = &self.rules.allowed_user_name_characters;
        if name.trim().is_empty() || (!allowed.is_empty() && name.chars().any(|c| !allowed.contains(c))) {
            return fail(
                "InvalidUserName",
                format!("Username '{name}' is invalid, can only contain letters or digits."),
            );
        }
        if let Some(owner) = find_by_name(self.tx, name).await?
            && owner.id != user.id {
                return fail("DuplicateUserName", format!("Username '{name}' is already taken."));
            }
        Ok(())
    }

    /// Validates, normalizes and saves the user, writing its audit entry.
    pub async fn update(&mut self, before: &StaffUser, mut after: StaffUser) -> UserResult<StaffUser> {
        self.validate_user(&after).await?;
        after.normalized_user_name = after.user_name.as_deref().map(normalize);
        after.normalized_email = after.email.as_deref().map(normalize);
        after.concurrency_stamp = Some(Uuid::new_v4().to_string());
        Ok(self.tx.update(before, after).await?)
    }

    async fn save(&mut self, user: &mut StaffUser, change: impl FnOnce(&mut StaffUser)) -> UserResult<()> {
        let before = user.clone();
        change(user);
        *user = self.update(&before, user.clone()).await?;
        Ok(())
    }

    pub async fn create(&mut self, new: NewUser, password: &str) -> UserResult<StaffUser> {
        self.validate_password(password)?;
        let user = StaffUser {
            id: Uuid::now_v7(),
            normalized_user_name: Some(normalize(&new.user_name)),
            user_name: Some(new.user_name),
            normalized_email: new.email.as_deref().map(normalize),
            email_confirmed: new.email.is_some(),
            email: new.email,
            password_hash: Some(password::hash(password)),
            security_stamp: Some(totp::new_base32_secret()),
            concurrency_stamp: Some(Uuid::new_v4().to_string()),
            phone_number: None,
            phone_number_confirmed: false,
            two_factor_enabled: false,
            lockout_end: None,
            lockout_enabled: self.rules.lockout_allowed_for_new_users,
            access_failed_count: 0,
            nickname: new.nickname,
            created_at: Ts::now(),
        };
        self.validate_user(&user).await?;
        Ok(self.tx.insert(user).await?)
    }

    pub async fn set_user_name(&mut self, user: &mut StaffUser, name: &str) -> UserResult<()> {
        self.save(user, |user| {
            user.user_name = Some(name.to_owned());
            user.security_stamp = Some(totp::new_base32_secret());
        })
        .await
    }

    /// Sets (or clears) the email. It starts unconfirmed.
    pub async fn set_email(&mut self, user: &mut StaffUser, email: Option<&str>) -> UserResult<()> {
        self.save(user, |user| {
            user.email = email.map(str::to_owned);
            user.email_confirmed = false;
            user.security_stamp = Some(totp::new_base32_secret());
        })
        .await
    }

    async fn role_by_name(&mut self, name: &str) -> UserResult<Role> {
        let role: Option<Role> = sqlx::query_as(r#"SELECT * FROM "AspNetRoles" WHERE "NormalizedName" = $1"#)
            .bind(normalize(name))
            .fetch_optional(&mut **self.tx)
            .await?;
        role.ok_or_else(|| UserError::Api(ApiError::internal(format!("Role {name} does not exist."))))
    }

    pub async fn add_to_roles<S: AsRef<str>>(&mut self, user: &mut StaffUser, names: &[S]) -> UserResult<()> {
        let current = roles_of(self.tx, user.id).await?;
        for name in names {
            let name = name.as_ref();
            let role = self.role_by_name(name).await?;
            if current.iter().any(|existing| normalize(existing) == normalize(name)) {
                return fail("UserAlreadyInRole", format!("User already in role '{name}'."));
            }
            self.tx.insert(UserRole { user_id: user.id, role_id: role.id }).await?;
        }
        self.save(user, |_| {}).await
    }

    pub async fn remove_from_roles<S: AsRef<str>>(&mut self, user: &mut StaffUser, names: &[S]) -> UserResult<()> {
        let current = roles_of(self.tx, user.id).await?;
        for name in names {
            let name = name.as_ref();
            let role = self.role_by_name(name).await?;
            if !current.iter().any(|existing| normalize(existing) == normalize(name)) {
                return fail("UserNotInRole", format!("User is not in role '{name}'."));
            }
            self.tx.delete(&UserRole { user_id: user.id, role_id: role.id }).await?;
        }
        self.save(user, |_| {}).await
    }

    /// Changes the password after checking the current one (`PasswordMismatch` otherwise).
    pub async fn change_password(&mut self, user: &mut StaffUser, current: &str, new: &str) -> UserResult<()> {
        if !check_password(user, current) {
            return fail("PasswordMismatch", "Incorrect password.");
        }
        self.reset_password(user, new).await
    }

    /// Sets a new password without knowing the old one, ending the user's other sessions.
    pub async fn reset_password(&mut self, user: &mut StaffUser, new: &str) -> UserResult<()> {
        self.validate_password(new)?;
        let hash = password::hash(new);
        self.save(user, |user| {
            user.password_hash = Some(hash);
            user.security_stamp = Some(totp::new_base32_secret());
        })
        .await
    }

    /// Gives a password to an account that has none.
    pub async fn add_password(&mut self, user: &mut StaffUser, new: &str) -> UserResult<()> {
        if user.has_password() {
            return fail("UserAlreadyHasPassword", "User already has a password set.");
        }
        self.reset_password(user, new).await
    }

    /// Counts a failed sign-in; enough of them lock the account for a while.
    pub async fn access_failed(&mut self, user: &mut StaffUser) -> UserResult<()> {
        let max = self.rules.lockout_max_failed_access_attempts;
        let span = chrono::Duration::from_std(self.rules.lockout_default_time_span).unwrap_or_default();
        self.save(user, |user| {
            user.access_failed_count += 1;
            if user.access_failed_count >= max {
                user.lockout_end = Some(Ts::now() + span);
                user.access_failed_count = 0;
            }
        })
        .await
    }

    pub async fn reset_access_failed_count(&mut self, user: &mut StaffUser) -> UserResult<()> {
        if user.access_failed_count == 0 {
            return Ok(());
        }
        self.save(user, |user| user.access_failed_count = 0).await
    }

    pub async fn set_lockout_enabled(&mut self, user: &mut StaffUser, enabled: bool) -> UserResult<()> {
        self.save(user, |user| user.lockout_enabled = enabled).await
    }

    pub async fn set_lockout_end(&mut self, user: &mut StaffUser, end: Option<Ts>) -> UserResult<()> {
        if !user.lockout_enabled {
            return fail("UserLockoutNotEnabled", "Lockout is not enabled for this user.");
        }
        self.save(user, |user| user.lockout_end = end).await
    }

    /// A new security stamp ends the user's existing sessions at their next validation.
    pub async fn update_security_stamp(&mut self, user: &mut StaffUser) -> UserResult<()> {
        self.save(user, |user| user.security_stamp = Some(totp::new_base32_secret())).await
    }

    pub async fn set_two_factor_enabled(&mut self, user: &mut StaffUser, enabled: bool) -> UserResult<()> {
        self.save(user, |user| {
            user.two_factor_enabled = enabled;
            user.security_stamp = Some(totp::new_base32_secret());
        })
        .await
    }

    async fn set_token(&mut self, user_id: Uuid, name: &str, value: String) -> UserResult<()> {
        match token(self.tx, user_id, name).await? {
            Some(existing) => {
                let mut changed = existing.clone();
                changed.value = Some(value);
                self.tx.update(&existing, changed).await?;
            }
            None => {
                self.tx
                    .insert(UserToken {
                        user_id,
                        login_provider: TOKEN_PROVIDER.to_owned(),
                        name: name.to_owned(),
                        value: Some(value),
                    })
                    .await?;
            }
        }
        Ok(())
    }

    /// Replaces the authenticator secret with a new one and returns it.
    pub async fn reset_authenticator_key(&mut self, user: &mut StaffUser) -> UserResult<String> {
        let key = totp::new_base32_secret();
        self.set_token(user.id, AUTHENTICATOR_KEY, key.clone()).await?;
        self.save(user, |user| user.security_stamp = Some(totp::new_base32_secret())).await?;
        Ok(key)
    }

    pub async fn verify_authenticator_code(&mut self, user: &StaffUser, code: &str) -> UserResult<bool> {
        let Some(key) = authenticator_key(self.tx, user.id).await? else {
            return Ok(false);
        };
        Ok(totp::verify(&key, code, Ts::now().unix_seconds()))
    }

    /// Replaces the user's recovery codes with `count` new one-time codes.
    pub async fn generate_recovery_codes(&mut self, user: &mut StaffUser, count: usize) -> UserResult<Vec<String>> {
        let mut codes: Vec<String> = Vec::with_capacity(count);
        while codes.len() < count {
            let code = new_recovery_code();
            if !codes.contains(&code) {
                codes.push(code);
            }
        }
        self.set_token(user.id, RECOVERY_CODES, codes.join(";")).await?;
        self.save(user, |_| {}).await?;
        Ok(codes)
    }

    /// Uses up a recovery code. False when the code is not one of the user's.
    pub async fn redeem_recovery_code(&mut self, user: &mut StaffUser, code: &str) -> UserResult<bool> {
        let stored = token(self.tx, user.id, RECOVERY_CODES).await?.and_then(|token| token.value).unwrap_or_default();
        let mut codes: Vec<&str> = stored.split(';').collect();
        let Some(position) = codes.iter().position(|candidate| *candidate == code) else {
            return Ok(false);
        };
        codes.remove(position);
        self.set_token(user.id, RECOVERY_CODES, codes.join(";")).await?;
        self.save(user, |_| {}).await?;
        Ok(true)
    }
}

/// `XXXXX-XXXXX` from an alphabet without look-alike characters.
fn new_recovery_code() -> String {
    let mut rng = rand::rng();
    let mut code = String::with_capacity(11);
    for index in 0..10 {
        if index == 5 {
            code.push('-');
        }
        code.push(RECOVERY_CODE_CHARS[rng.random_range(0..RECOVERY_CODE_CHARS.len())] as char);
    }
    code
}

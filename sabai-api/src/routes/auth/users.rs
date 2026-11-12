//! Staff administration: the directory, roles, locks and credential resets.

use axum::Router;
use axum::routing::{get, post};
use serde::{Deserialize, Serialize};
use sqlx::PgConnection;
use uuid::Uuid;

use super::UserResponse;
use crate::auth::users::{self, NewUser, StaffUser, Users, roles};
use crate::auth::{Admin, Staff};
use crate::db::Tx;
use crate::error::{ApiError, ApiResult, OrNotFound};
use crate::http::{Created, Ctx, Json, Req, Rules, Validate};
use crate::state::AppState;
use crate::time::Ts;

/// Identifies a staff user.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct IdRequest {
    #[serde(default)]
    pub id: Uuid,
}

impl Validate for IdRequest {}

async fn find(conn: &mut PgConnection, id: Uuid) -> ApiResult<StaffUser> {
    users::find_by_id(conn, id).await.or_not_found("Staff user was not found.")
}

fn known_roles(v: &mut Rules, roles: &[String]) {
    for (index, role) in roles.iter().enumerate() {
        v.must(
            &format!("roles[{index}]"),
            roles::is_known(role),
            format!("Roles must be one of: {}.", roles::ALL.join(", ")),
        );
    }
}

/// The first occurrence of each role, in the order given.
fn distinct(roles: &[String]) -> Vec<String> {
    let mut distinct: Vec<String> = Vec::new();
    for role in roles {
        if !distinct.contains(role) {
            distinct.push(role.clone());
        }
    }
    distinct
}

fn is_blank(text: &Option<String>) -> bool {
    text.as_deref().is_none_or(|text| text.trim().is_empty())
}

/// Role names that can be given to staff users.
#[derive(Debug, Serialize)]
pub struct RolesResponse {
    pub roles: Vec<String>,
}

/// List assignable roles.
async fn list_roles(ctx: Ctx, _: Admin) -> ApiResult<Json<RolesResponse>> {
    let roles =
        sqlx::query_scalar(r#"SELECT "Name" FROM "AspNetRoles" WHERE "Name" IS NOT NULL ORDER BY "Name""#)
            .fetch_all(ctx.db())
            .await?;
    Ok(Json(RolesResponse { roles }))
}

/// Optional filters for the staff directory.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ListRequest {
    /// Only users in this role (for example `admin`).
    pub role: Option<String>,
    pub email: Option<String>,
    pub is_locked: Option<bool>,
}

impl Validate for ListRequest {}

/// Staff directory page.
#[derive(Debug, Serialize)]
pub struct ListResponse {
    pub users: Vec<UserResponse>,
}

/// List staff users.
async fn list(ctx: Ctx, _: Staff, Req(req): Req<ListRequest>) -> ApiResult<Json<ListResponse>> {
    let role = req.role.as_deref().map(str::trim).filter(|role| !role.is_empty()).map(str::to_uppercase);
    let email = req.email.as_deref().map(str::trim).filter(|email| !email.is_empty()).map(str::to_uppercase);
    let mut conn = ctx.db().acquire().await?;
    let found: Vec<StaffUser> = sqlx::query_as(
        r#"SELECT u.* FROM "StaffUsers" u
           WHERE ($1::text IS NULL OR EXISTS (
                   SELECT 1 FROM "AspNetUserRoles" ur
                   WHERE ur."UserId" = u."Id" AND EXISTS (
                       SELECT 1 FROM "AspNetRoles" r WHERE r."Id" = ur."RoleId" AND r."NormalizedName" = $1)))
             AND ($2::text IS NULL OR u."NormalizedEmail" = $2)
             AND ($3::boolean IS NULL
                  OR (u."LockoutEnabled" AND u."LockoutEnd" IS NOT NULL AND u."LockoutEnd" > $4) = $3)
           ORDER BY u."Nickname", u."UserName""#,
    )
    .bind(role)
    .bind(email)
    .bind(req.is_locked)
    .bind(Ts::now())
    .fetch_all(&mut *conn)
    .await?;

    let ids: Vec<Uuid> = found.iter().map(|user| user.id).collect();
    let mut roles = users::roles_by_user(&mut conn, &ids).await?;
    let users = found
        .iter()
        .map(|user| UserResponse::from(user, roles.remove(&user.id).unwrap_or_default()))
        .collect();
    Ok(Json(ListResponse { users }))
}

/// Get a staff user.
async fn get_one(ctx: Ctx, _: Staff, Req(req): Req<IdRequest>) -> ApiResult<Json<UserResponse>> {
    let mut conn = ctx.db().acquire().await?;
    let user = find(&mut conn, req.id).await?;
    Ok(Json(UserResponse::load(&mut conn, &user).await?))
}

/// Admin payload for provisioning a staff account.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateRequest {
    pub user_name: String,
    /// Optional. Can also be used to sign in.
    pub email: Option<String>,
    pub password: String,
    /// Display name; defaults to the username.
    pub nickname: Option<String>,
    /// Role names from `GET /auth/roles`. Defaults to `member`.
    pub roles: Option<Vec<String>>,
}

impl Validate for CreateRequest {
    fn validate(&self, v: &mut Rules) {
        v.not_empty("userName", &self.user_name).max_len("userName", &self.user_name, 150);
        if !is_blank(&self.email) {
            v.email("email", &self.email).max_len("email", &self.email, 255);
        }
        v.not_empty("password", &self.password);
        v.max_len("nickname", &self.nickname, 255);
        known_roles(v, self.roles.as_deref().unwrap_or_default());
    }
}

/// Create a staff user.
async fn create(ctx: Ctx, _: Admin, Req(req): Req<CreateRequest>) -> ApiResult<Created<UserResponse>> {
    let user_name = req.user_name.trim();
    let email = req.email.as_deref().map(str::trim).filter(|email| !email.is_empty());
    let mut tx = ctx.begin().await?;
    if let Some(email) = email
        && users::find_by_email(&mut tx, email).await?.is_some() {
            return Err(ApiError::conflict("A user with this email already exists."));
        }

    let nickname = req.nickname.as_deref().map(str::trim).filter(|nickname| !nickname.is_empty()).unwrap_or(user_name);
    let new = NewUser {
        user_name: user_name.to_owned(),
        email: email.map(str::to_owned),
        nickname: nickname.to_owned(),
    };
    let mut accounts = Users::new(&mut tx, &ctx.state.settings.identity);
    let mut user = accounts.create(new, &req.password).await?;
    let roles = match req.roles.as_deref() {
        Some(roles) if !roles.is_empty() => distinct(roles),
        _ => vec![roles::MEMBER.to_owned()],
    };
    accounts.add_to_roles(&mut user, &roles).await?;

    let response = UserResponse::load(&mut tx, &user).await?;
    tx.commit().await?;
    Ok(Created::at(format!("/auth/users/{}", user.id), response))
}

/// Partial staff profile update. Omitted fields are left as they are.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateRequest {
    #[serde(default)]
    pub id: Uuid,
    pub user_name: Option<String>,
    /// An empty string removes the email.
    pub email: Option<String>,
    pub nickname: Option<String>,
    /// The full set of roles the user should have.
    pub roles: Option<Vec<String>>,
}

impl Validate for UpdateRequest {
    fn validate(&self, v: &mut Rules) {
        if self.user_name.is_some() {
            v.not_empty("userName", &self.user_name).max_len("userName", &self.user_name, 150);
        }
        if !is_blank(&self.email) {
            v.email("email", &self.email).max_len("email", &self.email, 255);
        }
        if self.nickname.is_some() {
            v.not_empty("nickname", &self.nickname).max_len("nickname", &self.nickname, 255);
        }
        v.must("roles", self.roles.as_ref().is_none_or(|roles| !roles.is_empty()), "A user needs at least one role.");
        known_roles(v, self.roles.as_deref().unwrap_or_default());
    }
}

async fn apply_update(ctx: &Ctx, tx: &mut Tx, user: &mut StaffUser, req: &UpdateRequest) -> ApiResult<()> {
    let rules = &ctx.state.settings.identity;
    if let Some(name) = req.user_name.as_deref().map(str::trim)
        && user.user_name.as_deref() != Some(name) {
            Users::new(tx, rules).set_user_name(user, name).await?;
        }

    let mut saved = user.clone();
    if let Some(email) = req.email.as_deref() {
        let email = Some(email.trim()).filter(|email| !email.is_empty());
        if let Some(email) = email
            && users::find_by_email(tx, email).await?.is_some_and(|other| other.id != user.id) {
                return Err(ApiError::conflict("A user with this email already exists."));
            }
        if email != user.email.as_deref() {
            Users::new(tx, rules).set_email(user, email).await?;
            saved = user.clone();
            user.email_confirmed = email.is_some();
        }
    }
    if let Some(nickname) = &req.nickname {
        user.nickname = nickname.trim().to_owned();
    }
    *user = Users::new(tx, rules).update(&saved, user.clone()).await?;

    if let Some(wanted) = &req.roles {
        let wanted = distinct(wanted);
        let current = users::roles_of(tx, user.id).await?;
        let has = |list: &[String], role: &str| list.iter().any(|candidate| candidate == role);
        let is_self = ctx.principal.as_ref().is_some_and(|principal| principal.id == user.id);
        if is_self && has(&current, roles::ADMIN) && !has(&wanted, roles::ADMIN) {
            return Err(ApiError::bad_request("You cannot remove your own admin role."));
        }
        let remove: Vec<&String> = current.iter().filter(|role| !wanted.contains(role)).collect();
        let add: Vec<&String> = wanted.iter().filter(|role| !current.contains(role)).collect();
        if !remove.is_empty() {
            Users::new(tx, rules).remove_from_roles(user, &remove).await?;
        }
        if !add.is_empty() {
            Users::new(tx, rules).add_to_roles(user, &add).await?;
        }
    }
    Ok(())
}

/// Update a staff user.
///
/// Role changes reach the user's open sessions within a minute, without signing them out.
async fn update(ctx: Ctx, _: Admin, Req(req): Req<UpdateRequest>) -> ApiResult<Json<UserResponse>> {
    let mut tx = ctx.begin().await?;
    let mut user = find(&mut tx, req.id).await?;
    // Each step is saved as it completes: the steps before a refused one stay applied.
    let applied = apply_update(&ctx, &mut tx, &mut user, &req).await;
    let response = match applied {
        Ok(()) => Ok(UserResponse::load(&mut tx, &user).await?),
        Err(error) => Err(error),
    };
    tx.commit().await?;
    response.map(Json)
}

/// User deletion is disabled.
async fn delete(_: Admin, Req(_): Req<IdRequest>) -> ApiResult<()> {
    Err(ApiError::bad_request("User deletion is disabled."))
}

/// Lock a staff account.
///
/// Blocks sign-in until unlocked and ends the user's open sessions within a minute.
async fn lock(ctx: Ctx, _: Admin, Req(req): Req<IdRequest>) -> ApiResult<Json<UserResponse>> {
    let mut tx = ctx.begin().await?;
    let mut user = find(&mut tx, req.id).await?;
    if ctx.principal.as_ref().is_some_and(|principal| principal.id == user.id) {
        return Err(ApiError::bad_request("You cannot lock your own account."));
    }
    let mut accounts = Users::new(&mut tx, &ctx.state.settings.identity);
    accounts.set_lockout_enabled(&mut user, true).await?;
    accounts.set_lockout_end(&mut user, Some(Ts::max())).await?;
    // A new security stamp invalidates existing cookies at their next validation.
    accounts.update_security_stamp(&mut user).await?;
    let response = UserResponse::load(&mut tx, &user).await?;
    tx.commit().await?;
    Ok(Json(response))
}

/// Unlock a staff account.
///
/// Also clears a temporary lock from too many failed sign-ins.
async fn unlock(ctx: Ctx, _: Admin, Req(req): Req<IdRequest>) -> ApiResult<Json<UserResponse>> {
    let mut tx = ctx.begin().await?;
    let mut user = find(&mut tx, req.id).await?;
    let mut accounts = Users::new(&mut tx, &ctx.state.settings.identity);
    accounts.set_lockout_end(&mut user, None).await?;
    accounts.reset_access_failed_count(&mut user).await?;
    let response = UserResponse::load(&mut tx, &user).await?;
    tx.commit().await?;
    Ok(Json(response))
}

/// A new password chosen by an admin.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ResetPasswordRequest {
    #[serde(default)]
    pub id: Uuid,
    pub password: String,
}

impl Validate for ResetPasswordRequest {
    fn validate(&self, v: &mut Rules) {
        v.not_empty("password", &self.password);
    }
}

/// Set a staff user's password.
///
/// Replaces the password (or sets one for accounts without), and ends the user's open
/// sessions within a minute.
async fn reset_password(ctx: Ctx, _: Admin, Req(req): Req<ResetPasswordRequest>) -> ApiResult<Json<UserResponse>> {
    let mut tx = ctx.begin().await?;
    let mut user = find(&mut tx, req.id).await?;
    Users::new(&mut tx, &ctx.state.settings.identity).reset_password(&mut user, &req.password).await?;
    let response = UserResponse::load(&mut tx, &user).await?;
    tx.commit().await?;
    Ok(Json(response))
}

/// Turn off a staff user's two-factor authentication.
///
/// For a lost phone: turns two-factor off and discards the authenticator secret, so the
/// user can sign in with just their password and set it up again.
async fn reset_two_factor(ctx: Ctx, _: Admin, Req(req): Req<IdRequest>) -> ApiResult<Json<UserResponse>> {
    let mut tx = ctx.begin().await?;
    let mut user = find(&mut tx, req.id).await?;
    let mut accounts = Users::new(&mut tx, &ctx.state.settings.identity);
    accounts.set_two_factor_enabled(&mut user, false).await?;
    accounts.reset_authenticator_key(&mut user).await?;
    let response = UserResponse::load(&mut tx, &user).await?;
    tx.commit().await?;
    Ok(Json(response))
}

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/auth/roles", get(list_roles))
        .route("/auth/users", get(list).post(create))
        .route("/auth/users/{id}", get(get_one).patch(update).delete(delete))
        .route("/auth/users/{id}/lock", post(lock))
        .route("/auth/users/{id}/unlock", post(unlock))
        .route("/auth/users/{id}/password", post(reset_password))
        .route("/auth/users/{id}/two-factor/reset", post(reset_two_factor))
}

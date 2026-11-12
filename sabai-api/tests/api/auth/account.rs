use reqwest::StatusCode;
use serde_json::{Value, json};

use super::{create_user, enable_two_factor, login_body, strings, totp};
use crate::support::*;

/// Passes the password step in a fresh browser and tries a recovery code.
async fn sign_in_with_recovery_code(user_name: &str, code: &str) -> StatusCode {
    let mut browser = Client::anonymous();
    browser.post("/auth/login", login_body(user_name, PASSWORD)).await.expect(StatusCode::OK);
    browser.post("/auth/login/recovery-code", json!({ "recoveryCode": code })).await.status
}

#[tokio::test]
async fn two_factor_setup_enable_then_sign_in_needs_code() {
    let (user_name, _) = create_user().await;
    let mut client = Client::signed_in(&user_name).await;

    // Set up the authenticator and confirm it with a code.
    let setup = client.post_empty("/auth/account/authenticator").await.expect(StatusCode::OK).json();
    let key = setup["sharedKey"].as_str().unwrap();
    let enable = client
        .post("/auth/account/two-factor/enable", json!({ "code": totp(key) }))
        .await
        .expect(StatusCode::OK)
        .json();
    let status = client.get("/auth/account/two-factor").await.expect(StatusCode::OK).json();
    client.get("/auth/whoami").await.expect(StatusCode::OK);

    // Sign in again from a fresh browser.
    let mut browser = Client::anonymous();
    let password = browser.post("/auth/login", login_body(&user_name, PASSWORD)).await.expect(StatusCode::OK);
    assert!(browser.has_cookie("Identity.TwoFactorUserId"));
    assert!(!browser.has_cookie("sabai.auth"));
    browser.get("/auth/whoami").await.expect(StatusCode::UNAUTHORIZED);
    browser.post("/auth/login/two-factor", json!({ "code": "000000" })).await.expect(StatusCode::UNAUTHORIZED);
    let right = browser
        .post("/auth/login/two-factor", json!({ "code": totp(key) }))
        .await
        .expect(StatusCode::OK)
        .json();

    let secret = key.replace(' ', "").to_uppercase();
    assert_eq!(
        setup["authenticatorUri"],
        format!("otpauth://totp/Sa%27Bai:{user_name}?secret={secret}&issuer=Sa%27Bai&digits=6").as_str()
    );
    assert_eq!(strings(&enable["recoveryCodes"]).len(), 10);
    assert_eq!(status, json!({ "isEnabled": true, "recoveryCodesLeft": 10, "isMachineRemembered": false }));
    assert_eq!(password.json(), json!({ "requiresTwoFactor": true, "user": null }));
    assert_eq!(right["requiresTwoFactor"], false);
    assert_eq!(right["user"]["userName"], user_name.as_str());
    assert!(!browser.has_cookie("Identity.TwoFactorUserId"));
    assert_eq!(browser.who_am_i().await["twoFactorEnabled"], true);
}

#[tokio::test]
async fn two_factor_recovery_code_works_once() {
    let (user_name, _) = create_user().await;
    let mut client = Client::signed_in(&user_name).await;
    let (_, codes) = enable_two_factor(&mut client).await;

    let first = sign_in_with_recovery_code(&user_name, &codes[0]).await;
    let reused = sign_in_with_recovery_code(&user_name, &codes[0]).await;

    assert_eq!(first, StatusCode::OK);
    assert_eq!(reused, StatusCode::UNAUTHORIZED);
    let status = client.get("/auth/account/two-factor").await.expect(StatusCode::OK).json();
    assert_eq!(status["recoveryCodesLeft"], 9);
}

#[tokio::test]
async fn two_factor_disable_needs_password_then_sign_in_is_password_only() {
    let (user_name, _) = create_user().await;
    let mut client = Client::signed_in(&user_name).await;
    enable_two_factor(&mut client).await;

    let wrong = client
        .post("/auth/account/two-factor/disable", json!({ "password": "wrong-password" }))
        .await
        .expect(StatusCode::BAD_REQUEST);
    client
        .post("/auth/account/two-factor/disable", json!({ "password": PASSWORD }))
        .await
        .expect(StatusCode::NO_CONTENT);
    let mut again = Client::signed_in(&user_name).await;

    assert_eq!(wrong.detail(), "Password is incorrect.");
    assert_eq!(again.who_am_i().await["twoFactorEnabled"], false);
    client.get("/auth/whoami").await.expect(StatusCode::OK);
}

#[tokio::test]
async fn remembered_browser_skips_the_code_until_two_factor_changes() {
    let (user_name, _) = create_user().await;
    let mut client = Client::signed_in(&user_name).await;
    let (key, _) = enable_two_factor(&mut client).await;

    let mut browser = Client::anonymous();
    browser.post("/auth/login", login_body(&user_name, PASSWORD)).await.expect(StatusCode::OK);
    browser
        .post("/auth/login/two-factor", json!({ "code": totp(&key), "rememberMachine": true }))
        .await
        .expect(StatusCode::OK);
    let status = browser.get("/auth/account/two-factor").await.expect(StatusCode::OK).json();
    browser.post_empty("/auth/logout").await.expect(StatusCode::NO_CONTENT);
    let remembered = browser.post("/auth/login", login_body(&user_name, PASSWORD)).await.expect(StatusCode::OK).json();

    // Turning two-factor off forgets this browser.
    browser.post("/auth/account/two-factor/disable", json!({ "password": PASSWORD })).await.expect(StatusCode::NO_CONTENT);
    assert!(!browser.has_cookie("Identity.TwoFactorRememberMe"));
    enable_two_factor(&mut browser).await;
    browser.post_empty("/auth/logout").await.expect(StatusCode::NO_CONTENT);
    let asked_again = browser.post("/auth/login", login_body(&user_name, PASSWORD)).await.expect(StatusCode::OK).json();

    assert_eq!(status["isMachineRemembered"], true);
    assert_eq!(remembered["requiresTwoFactor"], false);
    assert_eq!(asked_again["requiresTwoFactor"], true);
}

#[tokio::test]
async fn admin_reset_two_factor_lets_user_sign_in_with_password() {
    let (user_name, id) = create_user().await;
    let mut client = Client::signed_in(&user_name).await;
    enable_two_factor(&mut client).await;
    let mut admin = Client::admin().await;

    let user = admin.post_empty(&format!("/auth/users/{id}/two-factor/reset")).await.expect(StatusCode::OK).json();
    let mut again = Client::signed_in(&user_name).await;

    assert_eq!(user["twoFactorEnabled"], false);
    assert_eq!(again.who_am_i().await["userName"], user_name.as_str());
}

#[tokio::test]
async fn change_password_old_password_stops_working() {
    let (user_name, _) = create_user().await;
    let mut client = Client::signed_in(&user_name).await;
    let mut other_session = Client::signed_in(&user_name).await;
    let new_password = "a-brand-new-password";

    let wrong_current = client
        .post("/auth/account/password", json!({ "currentPassword": "nope-nope-nope", "newPassword": new_password }))
        .await
        .expect(StatusCode::BAD_REQUEST);
    let too_short = client
        .post("/auth/account/password", json!({ "currentPassword": PASSWORD, "newPassword": "short" }))
        .await
        .expect(StatusCode::BAD_REQUEST);
    client
        .post("/auth/account/password", json!({ "currentPassword": PASSWORD, "newPassword": new_password }))
        .await
        .expect(StatusCode::NO_CONTENT);
    client.get("/auth/whoami").await.expect(StatusCode::OK);
    other_session.get("/auth/whoami").await.expect(StatusCode::UNAUTHORIZED);
    Client::anonymous().post("/auth/login", login_body(&user_name, PASSWORD)).await.expect(StatusCode::UNAUTHORIZED);
    let mut with_new = Client::anonymous();
    with_new.login(&user_name, new_password).await;

    assert_eq!(wrong_current.detail(), "Current password is incorrect.");
    assert_eq!(too_short.detail(), "Passwords must be at least 8 characters.");
    assert_eq!(with_new.who_am_i().await["userName"], user_name.as_str());
}

#[tokio::test]
async fn admin_set_password_ends_open_session_and_new_password_works() {
    let (user_name, id) = create_user().await;
    let mut session = Client::signed_in(&user_name).await;
    let mut admin = Client::admin().await;
    let new_password = "set-by-the-admin";
    let path = format!("/auth/users/{id}/password");

    let too_short = admin.post(&path, json!({ "password": "short" })).await.expect(StatusCode::BAD_REQUEST);
    admin.post(&path, json!({ "password": new_password })).await.expect(StatusCode::OK);
    session.get("/auth/whoami").await.expect(StatusCode::UNAUTHORIZED);
    let mut with_new = Client::anonymous();
    with_new.login(&user_name, new_password).await;

    assert_eq!(too_short.detail(), "Passwords must be at least 8 characters.");
    assert!(!session.has_cookie("sabai.auth"));
    assert_eq!(with_new.who_am_i().await["userName"], user_name.as_str());
}

#[tokio::test]
async fn lock_ends_open_session() {
    let (user_name, id) = create_user().await;
    let mut session = Client::signed_in(&user_name).await;
    let mut admin = Client::admin().await;

    let locked = admin.post_empty(&format!("/auth/users/{id}/lock")).await.expect(StatusCode::OK).json();

    assert_eq!(locked["isLocked"], true);
    session.get("/auth/whoami").await.expect(StatusCode::UNAUTHORIZED);
}

#[tokio::test]
async fn role_change_reaches_open_session_without_sign_out() {
    let (user_name, id) = create_user().await;
    let mut session = Client::signed_in(&user_name).await;
    let mut admin = Client::admin().await;
    let path = format!("/auth/users/{id}");
    session.get("/auth/roles").await.expect(StatusCode::FORBIDDEN);

    let promoted = admin.patch(&path, json!({ "roles": ["admin", "member"] })).await.expect(StatusCode::OK).json();
    session.get("/auth/roles").await.expect(StatusCode::OK);
    let demoted = admin.patch(&path, json!({ "roles": ["member"] })).await.expect(StatusCode::OK).json();
    session.get("/auth/roles").await.expect(StatusCode::FORBIDDEN);

    assert_eq!(promoted["roles"], json!(["admin", "member"]));
    assert_eq!(demoted["roles"], json!(["member"]));
    session.get("/auth/whoami").await.expect(StatusCode::OK);
}

#[tokio::test]
async fn admin_cannot_lock_self_or_drop_own_admin_role() {
    let mut admin = Client::admin().await;
    let me = admin.who_am_i().await;
    let id = me["id"].as_str().unwrap();

    let lock_self = admin.post_empty(&format!("/auth/users/{id}/lock")).await.expect(StatusCode::BAD_REQUEST);
    let demote_self = admin
        .patch(&format!("/auth/users/{id}"), json!({ "roles": ["member"] }))
        .await
        .expect(StatusCode::BAD_REQUEST);

    assert_eq!(lock_self.detail(), "You cannot lock your own account.");
    assert_eq!(demote_self.detail(), "You cannot remove your own admin role.");
    assert!(admin.who_am_i().await["roles"].as_array().unwrap().contains(&json!("admin")));
}

#[tokio::test]
async fn repeated_failed_sign_ins_lock_the_account() {
    let (user_name, _) = create_user().await;
    let mut client = Client::anonymous();

    let mut attempts = Vec::new();
    for _ in 0..5 {
        attempts.push(client.post("/auth/login", login_body(&user_name, "wrong-password")).await.status);
    }
    let with_right_password = client.post("/auth/login", login_body(&user_name, PASSWORD)).await;
    let mut admin = Client::admin().await;
    let listed = admin.get("/auth/users?isLocked=true").await.expect(StatusCode::OK).json();

    // The attempt that reaches the limit is itself answered as locked.
    let rejected = StatusCode::UNAUTHORIZED;
    assert_eq!(attempts, [rejected, rejected, rejected, rejected, StatusCode::FORBIDDEN]);
    with_right_password.expect(StatusCode::FORBIDDEN);
    let locked = listed["users"]
        .as_array()
        .unwrap()
        .iter()
        .find(|user| user["userName"] == user_name.as_str())
        .expect("the locked account is listed");
    assert_eq!(locked["isLocked"], true);
    assert_ne!(locked["lockoutEnd"], Value::Null);
}

#[tokio::test]
async fn logout_clears_session() {
    let mut client = Client::signed_in(DOCTOR).await;

    client.post_empty("/auth/logout").await.expect(StatusCode::NO_CONTENT);

    assert!(!client.has_cookie("sabai.auth"));
    client.get("/auth/whoami").await.expect(StatusCode::UNAUTHORIZED);
}

#[tokio::test]
async fn roles_lists_admin_and_member() {
    let mut admin = Client::admin().await;

    let roles = admin.get("/auth/roles").await.expect(StatusCode::OK).json();

    assert_eq!(roles, json!({ "roles": ["admin", "member"] }));
}

#[tokio::test]
async fn create_without_email_can_sign_in_by_user_name() {
    let mut admin = Client::admin().await;
    let user_name = format!("noemail{}", suffix());

    let created = admin.post("/auth/users", login_body(&user_name, PASSWORD)).await.expect(StatusCode::CREATED).json();
    let me = Client::signed_in(&user_name).await.who_am_i().await;

    assert_eq!(created["email"], Value::Null);
    assert_eq!(created["roles"], json!(["member"]));
    assert_eq!(me["nickname"], user_name.as_str());
}

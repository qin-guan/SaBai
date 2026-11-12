use reqwest::StatusCode;
use serde_json::{Value, json};

use super::login_body;
use crate::support::*;

#[tokio::test]
async fn healthcheck_returns_yay() {
    let mut client = Client::anonymous();
    let response = client.get("/healthcheck").await.expect(StatusCode::OK);
    assert!(response.text().contains("yay!"));
}

#[tokio::test]
async fn login_with_seeded_admin_sets_session_cookie_and_who_am_i() {
    let mut client = Client::anonymous();
    client.login(ADMIN, PASSWORD).await;
    assert!(client.has_cookie("sabai.auth"));
    let me = client.who_am_i().await;

    assert_eq!(me["email"], ADMIN);
    assert_eq!(me["userName"], "admin");
    assert!(me["roles"].as_array().unwrap().contains(&json!("admin")));
    assert_eq!(me["hasPassword"], true);
    assert_eq!(me["isLocked"], false);
    assert_eq!(me["lockoutEnd"], Value::Null);
}

#[tokio::test]
async fn login_with_wrong_password_returns_unauthorized() {
    // A throwaway account, so the failed attempt cannot race with the shared admin's
    // sign-ins (a successful sign-in resets the failure count).
    let mut admin = Client::admin().await;
    let user_name = format!("wrongpw{}", suffix());
    admin.post("/auth/users", login_body(&user_name, PASSWORD)).await.expect(StatusCode::CREATED);
    let mut client = Client::anonymous();

    let response =
        client.post("/auth/login", login_body(&user_name, "not-the-password")).await.expect(StatusCode::UNAUTHORIZED);

    assert_eq!(response.detail(), "Invalid credentials.");
    assert!(!client.has_cookie("sabai.auth"));
}

#[tokio::test]
async fn who_am_i_without_session_returns_unauthorized() {
    let mut client = Client::anonymous();
    let response = client.get("/auth/whoami").await.expect(StatusCode::UNAUTHORIZED);
    assert!(response.bytes.is_empty());
}

#[tokio::test]
async fn create_user_as_member_returns_forbidden() {
    let mut client = Client::signed_in(DOCTOR).await;

    let response = client
        .post(
            "/auth/users",
            json!({
                "userName": format!("member{}", suffix()),
                "email": format!("nope-{}@sabai.test", suffix()),
                "password": PASSWORD,
            }),
        )
        .await;

    response.expect(StatusCode::FORBIDDEN);
}

#[tokio::test]
async fn create_lock_and_unlock_prevents_then_allows_login() {
    let mut admin = Client::admin().await;
    let suffix = suffix();
    let user_name = format!("lock{suffix}");

    let created = admin
        .post(
            "/auth/users",
            json!({
                "userName": user_name,
                "email": format!("lock-{suffix}@sabai.test"),
                "password": PASSWORD,
                "nickname": "Locked User",
                "roles": ["member"],
            }),
        )
        .await
        .expect(StatusCode::CREATED);
    let user = created.json();
    let id = user["id"].as_str().unwrap();
    assert_eq!(created.header("location"), Some(format!("/auth/users/{id}").as_str()));
    assert_eq!(user["nickname"], "Locked User");

    let locked = admin.post_empty(&format!("/auth/users/{id}/lock")).await.expect(StatusCode::OK).json();
    assert_eq!(locked["isLocked"], true);
    assert_eq!(locked["lockoutEnd"], "9999-12-31T23:59:59.9999999+00:00");

    let denied =
        Client::anonymous().post("/auth/login", login_body(&user_name, PASSWORD)).await.expect(StatusCode::FORBIDDEN);
    assert_eq!(denied.detail(), "This account is locked. Try again later or ask an admin to unlock it.");

    let unlocked = admin.post_empty(&format!("/auth/users/{id}/unlock")).await.expect(StatusCode::OK).json();
    assert_eq!(unlocked["isLocked"], false);
    assert_eq!(unlocked["lockoutEnd"], Value::Null);

    let mut allowed = Client::signed_in(&user_name).await;
    assert_eq!(allowed.who_am_i().await["userName"], user_name.as_str());
}

#[tokio::test]
async fn delete_user_returns_bad_request() {
    let mut client = Client::admin().await;
    let me = client.who_am_i().await;

    let response =
        client.delete(&format!("/auth/users/{}", me["id"].as_str().unwrap())).await.expect(StatusCode::BAD_REQUEST);

    assert_eq!(response.detail(), "User deletion is disabled.");
}

use reqwest::StatusCode;
use serde_json::json;

use super::login_body;
use crate::support::*;

#[tokio::test]
async fn list_includes_seeded_admin() {
    let mut client = Client::admin().await;

    let response = client.get("/auth/users").await.expect(StatusCode::OK);

    assert!(response.text().contains(ADMIN));
}

#[tokio::test]
async fn list_is_ordered_by_nickname_then_user_name_and_filters() {
    let mut client = Client::admin().await;
    let suffix = suffix();
    for (user_name, nickname) in [("c", "B"), ("b", "A"), ("a", "B")] {
        client
            .post(
                "/auth/users",
                json!({
                    "userName": format!("order{suffix}{user_name}"),
                    "email": format!("order{suffix}{user_name}@sabai.test"),
                    "nickname": format!("Order {suffix} {nickname}"),
                    "password": PASSWORD,
                    "roles": if user_name == "a" { json!(["admin", "member"]) } else { json!(["member"]) },
                }),
            )
            .await
            .expect(StatusCode::CREATED);
    }
    let mine = |list: serde_json::Value| -> Vec<String> {
        list["users"]
            .as_array()
            .unwrap()
            .iter()
            .filter_map(|user| user["userName"].as_str()?.strip_prefix(&format!("order{suffix}")).map(str::to_owned))
            .collect()
    };

    let everyone = client.get("/auth/users").await.expect(StatusCode::OK).json();
    let admins = client.get("/auth/users?role=%20ADMIN&isLocked=false").await.expect(StatusCode::OK).json();
    let by_email =
        client.get(&format!("/auth/users?email=ORDER{suffix}c@sabai.test")).await.expect(StatusCode::OK).json();
    let locked = client.get("/auth/users?isLocked=true").await.expect(StatusCode::OK).json();

    assert_eq!(mine(everyone), ["b", "a", "c"]);
    assert_eq!(mine(admins), ["a"]);
    assert_eq!(by_email["users"].as_array().unwrap().len(), 1);
    assert_eq!(by_email["users"][0]["roles"], json!(["member"]));
    assert_eq!(mine(locked), Vec::<String>::new());
}

#[tokio::test]
async fn get_signed_in_admin_returns_email() {
    let mut client = Client::admin().await;
    let me = client.who_am_i().await;

    let user = client.get(&format!("/auth/users/{}", me["id"].as_str().unwrap())).await.expect(StatusCode::OK).json();

    assert_eq!(user["email"], ADMIN);
    assert_eq!(user, me);
}

#[tokio::test]
async fn patch_nickname_is_returned_on_get() {
    let mut client = Client::admin().await;
    let suffix = suffix();
    let nickname = format!("Nick {suffix}");
    let created = client
        .post(
            "/auth/users",
            json!({
                "userName": format!("user{suffix}"),
                "email": format!("user-{suffix}@sabai.test"),
                "password": PASSWORD,
                "nickname": "Before",
            }),
        )
        .await
        .expect(StatusCode::CREATED)
        .json();
    let path = format!("/auth/users/{}", created["id"].as_str().unwrap());

    let patched = client.patch(&path, json!({ "nickname": nickname })).await.expect(StatusCode::OK).json();
    let fetched = client.get(&path).await.expect(StatusCode::OK).json();

    assert_eq!(patched["nickname"], nickname.as_str());
    assert_eq!(fetched["nickname"], nickname.as_str());
    assert_eq!(fetched["email"], created["email"]);
}

#[tokio::test]
async fn login_empty_password_returns_bad_request() {
    let mut client = Client::anonymous();

    let response = client.post("/auth/login", login_body("admin", "")).await.expect(StatusCode::BAD_REQUEST);

    assert_eq!(response.errors(), [("password".to_owned(), "'password' must not be empty.".to_owned())]);
}

#[tokio::test]
async fn login_unknown_user_returns_unauthorized() {
    let mut client = Client::anonymous();

    let response = client.post("/auth/login", login_body(&format!("missing-{}", suffix()), PASSWORD)).await;

    response.expect(StatusCode::UNAUTHORIZED);
}

#[tokio::test]
async fn lock_as_member_returns_forbidden() {
    let mut client = Client::signed_in(DOCTOR).await;

    let response = client.post_empty(&format!("/auth/users/{}/lock", uuid::Uuid::new_v4())).await;

    response.expect(StatusCode::FORBIDDEN);
}

#[tokio::test]
async fn create_duplicate_admin_email_returns_conflict() {
    let mut client = Client::admin().await;

    let response = client
        .post(
            "/auth/users",
            json!({ "userName": format!("dup{}", suffix()), "email": ADMIN, "password": PASSWORD }),
        )
        .await
        .expect(StatusCode::CONFLICT);

    assert_eq!(response.detail(), "A user with this email already exists.");
}

#[tokio::test]
async fn get_missing_user_returns_not_found() {
    let mut client = Client::admin().await;

    let response =
        client.get("/auth/users/00000000-0000-0000-0000-000000000099").await.expect(StatusCode::NOT_FOUND);

    assert_eq!(response.detail(), "Staff user was not found.");
}

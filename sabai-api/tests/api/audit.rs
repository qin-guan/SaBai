use reqwest::StatusCode;
use serde_json::{Value, json};

use crate::support::*;

fn change<'a>(entry: &'a Value, field: &str) -> &'a Value {
    entry["changes"]
        .as_array()
        .unwrap()
        .iter()
        .find(|change| change["field"] == field)
        .unwrap_or_else(|| panic!("no change for {field} in {entry}"))
}

#[tokio::test]
async fn record_changes_are_logged_with_user_and_old_and_new_values() {
    let mut client = Client::admin().await;
    let me = client.who_am_i().await;
    let code = village_code();
    let village = create_village_named(&mut client, &code, "Before").await;
    let path = format!("/village-codes/{}", id(&village));

    client.patch(&path, json!({ "name": "After" })).await.ok();
    client.delete(&path).await.ok();
    let log = client
        .get(&format!("/audit-logs?entityType=VillageCode&entityId={}", id(&village)))
        .await
        .ok()
        .json();

    let entries = log["entries"].as_array().unwrap();
    let actions: Vec<_> = entries.iter().map(|entry| entry["action"].as_str().unwrap()).collect();
    assert_eq!(actions, ["Deleted", "Updated", "Created"]);
    assert!(entries.iter().all(|entry| entry["userId"] == me["id"] && entry["userName"] == me["userName"]));

    let created = &entries[2];
    assert_eq!(change(created, "Code")["new"], code);
    assert_eq!(change(created, "Code")["old"], Value::Null);
    assert_eq!(created["request"], "POST /village-codes");
    assert!(created["changes"].as_array().unwrap().iter().all(|c| c["field"] != "Id"));

    let updated = &entries[1];
    assert_eq!(change(updated, "Name"), &json!({ "field": "Name", "old": "Before", "new": "After" }));
    let fields: Vec<_> = updated["changes"].as_array().unwrap().iter().map(|c| c["field"].as_str().unwrap()).collect();
    assert_eq!(fields, ["Name", "UpdatedAt"]);

    assert_eq!(change(&entries[0], "Name")["old"], "After");
}

#[tokio::test]
async fn sign_ins_failures_and_sign_out_are_logged() {
    let mut admin = Client::admin().await;
    let mut anonymous = Client::anonymous();
    let unknown = format!("ghost-{}", suffix());

    anonymous
        .post("/auth/login", json!({ "userName": "registration", "password": "wrong-password" }))
        .await
        .expect(StatusCode::UNAUTHORIZED);
    let rejected = anonymous
        .post("/auth/login", json!({ "userName": unknown, "password": "wrong-password" }))
        .await
        .expect(StatusCode::UNAUTHORIZED);
    assert_eq!(
        rejected.json(),
        json!({ "title": "Unauthorized", "status": 401, "detail": "Invalid credentials.", "instance": "/auth/login" })
    );

    let mut registration = Client::signed_in(REGISTRATION).await;
    let me = registration.who_am_i().await;
    registration.post_empty("/auth/logout").await.expect(StatusCode::NO_CONTENT);
    assert!(!registration.has_cookie("sabai.auth"));
    registration.get("/auth/whoami").await.expect(StatusCode::UNAUTHORIZED);

    let mine = admin.get(&format!("/audit-logs?userId={}&take=200", me["id"].as_str().unwrap())).await.ok().json();
    let mine: Vec<_> = mine["entries"].as_array().unwrap().iter().map(|e| e["action"].as_str().unwrap().to_owned()).collect();
    assert!(mine.contains(&"SignedIn".to_owned()));
    assert!(mine.contains(&"SignedOut".to_owned()));
    assert!(mine.contains(&"SignInFailed".to_owned()));

    let failures = admin.get("/audit-logs?action=SignInFailed&take=200").await.ok().json();
    let ghost = failures["entries"]
        .as_array()
        .unwrap()
        .iter()
        .find(|entry| entry["userName"] == unknown.as_str())
        .expect("the unknown user's attempt is logged");
    assert_eq!(ghost["userId"], Value::Null);
    assert_eq!(ghost["details"], "Password: unknown user");
    assert_eq!(ghost["changes"], json!([]));
}

#[tokio::test]
async fn secrets_are_redacted() {
    let mut admin = Client::admin().await;
    let user_name = format!("audit{}", suffix());
    let user = admin
        .post(
            "/auth/users",
            json!({ "userName": user_name, "nickname": "Audit Target", "password": "Password1!", "roles": ["member"] }),
        )
        .await
        .expect(StatusCode::CREATED)
        .json();
    let user_id = user["id"].as_str().unwrap();

    admin.post(&format!("/auth/users/{user_id}/password"), json!({ "password": "Another-Password1!" })).await.ok();
    let log = admin.get(&format!("/audit-logs?entityType=StaffUser&entityId={user_id}")).await.ok().json();

    let entries = log["entries"].as_array().unwrap();
    let actions: Vec<_> = entries.iter().map(|entry| entry["action"].as_str().unwrap()).collect();
    assert_eq!(actions, ["Updated", "Created"]);
    let secrets: Vec<&Value> = entries
        .iter()
        .flat_map(|entry| entry["changes"].as_array().unwrap())
        .filter(|change| change["field"] == "PasswordHash" || change["field"] == "SecurityStamp")
        .collect();
    assert_eq!(secrets.len(), 4);
    let redacted = |value: &Value| value.is_null() || value == "[redacted]";
    assert!(secrets.iter().all(|change| redacted(&change["old"]) && redacted(&change["new"])));
    assert_eq!(
        change(&entries[0], "PasswordHash"),
        &json!({ "field": "PasswordHash", "old": "[redacted]", "new": "[redacted]" })
    );
    assert_eq!(change(&entries[1], "UserName")["new"], user_name.as_str());
    assert!(entries[1]["changes"].as_array().unwrap().iter().all(|c| c["field"] != "ConcurrencyStamp"));
}

#[tokio::test]
async fn audit_log_is_admin_only_and_validates_its_filters() {
    let mut doctor = Client::signed_in(DOCTOR).await;
    let forbidden = doctor.get("/audit-logs").await.expect(StatusCode::FORBIDDEN);
    assert!(forbidden.bytes.is_empty());
    Client::anonymous().get("/audit-logs").await.expect(StatusCode::UNAUTHORIZED);

    let mut admin = Client::admin().await;
    let invalid = admin.get("/audit-logs?take=0&from=2026-01-02&to=2026-01-01").await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(
        invalid.errors(),
        [
            ("take".to_owned(), "'take' must be between 1 and 200. You entered 0.".to_owned()),
            ("to".to_owned(), "'to' must be after 'from'.".to_owned()),
        ]
    );
    let bogus = admin.get("/audit-logs?action=bogus").await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(
        bogus.errors(),
        [("action".to_owned(), "Value [bogus] is not valid for a [AuditAction] property!".to_owned())]
    );
}

#[tokio::test]
async fn audit_log_pages_with_a_cursor() {
    let mut admin = Client::admin().await;
    for _ in 0..3 {
        create_village(&mut admin).await;
    }
    let first = admin.get("/audit-logs?entityType=VillageCode&take=2").await.ok().json();
    assert_eq!(first["entries"].as_array().unwrap().len(), 2);
    let cursor = first["nextBefore"].as_i64().expect("more pages");
    assert_eq!(cursor, first["entries"][1]["id"].as_i64().unwrap());
    let second = admin.get(&format!("/audit-logs?entityType=VillageCode&take=2&before={cursor}")).await.ok().json();
    assert!(second["entries"].as_array().unwrap().iter().all(|entry| entry["id"].as_i64().unwrap() < cursor));
}

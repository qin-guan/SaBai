//! Sign-in, staff administration and the signed-in user's own account.

mod account;
mod sign_in;
mod user_admin;

use reqwest::StatusCode;
use serde_json::{Value, json};

use crate::support::*;

/// A new member with the demo password and an email: `(user name, id)`.
async fn create_user() -> (String, String) {
    let mut admin = Client::admin().await;
    let user_name = format!("staff{}", suffix());
    let user = admin
        .post(
            "/auth/users",
            json!({
                "userName": user_name,
                "email": format!("{user_name}@sabai.test"),
                "password": PASSWORD,
                "roles": ["member"],
            }),
        )
        .await
        .expect(StatusCode::CREATED)
        .json();
    (user_name, user["id"].as_str().unwrap().to_owned())
}

/// The code an authenticator app shows right now for `shared_key`.
fn totp(shared_key: &str) -> String {
    let now = std::time::SystemTime::now().duration_since(std::time::UNIX_EPOCH).unwrap().as_secs();
    sabai_api::auth::totp::generate(&shared_key.replace(' ', ""), now as i64).expect("a Base32 key")
}

/// Sets up an authenticator for the signed-in user and turns two-factor on.
/// Returns the shared key and the recovery codes.
async fn enable_two_factor(client: &mut Client) -> (String, Vec<String>) {
    let setup = client.post_empty("/auth/account/authenticator").await.expect(StatusCode::OK).json();
    let key = setup["sharedKey"].as_str().unwrap().to_owned();
    let enabled = client
        .post("/auth/account/two-factor/enable", json!({ "code": totp(&key) }))
        .await
        .expect(StatusCode::OK)
        .json();
    (key, strings(&enabled["recoveryCodes"]))
}

fn strings(value: &Value) -> Vec<String> {
    value.as_array().unwrap().iter().map(|item| item.as_str().unwrap().to_owned()).collect()
}

fn login_body(user_name: &str, password: &str) -> Value {
    json!({ "userName": user_name, "password": password })
}

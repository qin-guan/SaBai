//! A client with its own session. Cookies are kept per client (like a browser), so
//! tests can act as several users at once.

use std::collections::BTreeMap;

use reqwest::header::{COOKIE, SET_COOKIE};
use reqwest::{Method, StatusCode};
use serde_json::{Value, json};

use super::{ADMIN, PASSWORD, app};

pub struct Client {
    http: reqwest::Client,
    base: String,
    cookies: BTreeMap<String, String>,
}

#[derive(Debug)]
pub struct Response {
    pub status: StatusCode,
    pub headers: reqwest::header::HeaderMap,
    pub bytes: Vec<u8>,
}

impl Response {
    /// The body as JSON; `Null` when it is empty or not JSON.
    pub fn json(&self) -> Value {
        serde_json::from_slice(&self.bytes).unwrap_or(Value::Null)
    }

    pub fn text(&self) -> String {
        String::from_utf8_lossy(&self.bytes).into_owned()
    }

    pub fn header(&self, name: &str) -> Option<&str> {
        self.headers.get(name).and_then(|value| value.to_str().ok())
    }

    pub fn is_success(&self) -> bool {
        self.status.is_success()
    }

    /// Panics with the body unless the status is the expected one.
    #[track_caller]
    pub fn expect(self, status: StatusCode) -> Self {
        assert_eq!(self.status, status, "unexpected status; body: {}", self.text());
        self
    }

    #[track_caller]
    pub fn ok(self) -> Self {
        assert!(self.status.is_success(), "request failed: {} {}", self.status, self.text());
        self
    }

    /// The problem `detail` of an error response.
    pub fn detail(&self) -> String {
        self.json()["detail"].as_str().unwrap_or_default().to_owned()
    }

    /// `[(name, reason)]` of a validation problem.
    pub fn errors(&self) -> Vec<(String, String)> {
        self.json()["errors"]
            .as_array()
            .map(|errors| {
                errors
                    .iter()
                    .map(|e| (e["name"].as_str().unwrap_or("").to_owned(), e["reason"].as_str().unwrap_or("").to_owned()))
                    .collect()
            })
            .unwrap_or_default()
    }
}

impl Client {
    /// A client with no session.
    pub fn anonymous() -> Self {
        Client {
            http: reqwest::Client::builder().redirect(reqwest::redirect::Policy::none()).build().unwrap(),
            base: format!("http://{}", app().address),
            cookies: BTreeMap::new(),
        }
    }

    /// A client signed in as the demo admin.
    pub async fn admin() -> Self {
        Self::signed_in(ADMIN).await
    }

    /// A client signed in as `user_name` (username or email) with the demo password.
    pub async fn signed_in(user_name: &str) -> Self {
        let mut client = Self::anonymous();
        client.login(user_name, PASSWORD).await;
        client
    }

    /// Signs in and keeps the session cookie. Panics if a second factor is needed.
    pub async fn login(&mut self, user_name: &str, password: &str) {
        let response = self.post("/auth/login", json!({ "userName": user_name, "password": password })).await.ok();
        assert_eq!(response.json()["requiresTwoFactor"], false, "{user_name} needs a two-factor code");
    }

    pub fn url(&self, path: &str) -> String {
        format!("{}{path}", self.base)
    }

    pub fn has_cookie(&self, name: &str) -> bool {
        self.cookies.contains_key(name)
    }

    pub fn clear_cookies(&mut self) {
        self.cookies.clear();
    }

    /// A request builder carrying this client's cookies, for calls the helpers do not cover.
    pub fn request(&self, method: Method, path: &str) -> reqwest::RequestBuilder {
        let mut request = self.http.request(method, self.url(path));
        if !self.cookies.is_empty() {
            let header = self.cookies.iter().map(|(k, v)| format!("{k}={v}")).collect::<Vec<_>>().join("; ");
            request = request.header(COOKIE, header);
        }
        request
    }

    /// Sends the request and applies any `Set-Cookie` it returns to this client.
    pub async fn send(&mut self, request: reqwest::RequestBuilder) -> Response {
        let response = request.send().await.expect("the request could not be sent");
        for value in response.headers().get_all(SET_COOKIE) {
            let value = value.to_str().unwrap_or_default();
            let pair = value.split(';').next().unwrap_or_default();
            let (name, cookie) = pair.split_once('=').unwrap_or((pair, ""));
            if value.to_ascii_lowercase().contains("expires=thu, 01 jan 1970") {
                self.cookies.remove(name);
            } else {
                self.cookies.insert(name.to_owned(), cookie.to_owned());
            }
        }
        Response {
            status: response.status(),
            headers: response.headers().clone(),
            bytes: response.bytes().await.unwrap_or_default().to_vec(),
        }
    }

    pub async fn get(&mut self, path: &str) -> Response {
        let request = self.request(Method::GET, path);
        self.send(request).await
    }

    pub async fn delete(&mut self, path: &str) -> Response {
        let request = self.request(Method::DELETE, path);
        self.send(request).await
    }

    pub async fn post(&mut self, path: &str, body: Value) -> Response {
        let request = self.request(Method::POST, path).json(&body);
        self.send(request).await
    }

    /// `POST` without a body.
    pub async fn post_empty(&mut self, path: &str) -> Response {
        let request = self.request(Method::POST, path);
        self.send(request).await
    }

    pub async fn patch(&mut self, path: &str, body: Value) -> Response {
        let request = self.request(Method::PATCH, path).json(&body);
        self.send(request).await
    }

    pub async fn put(&mut self, path: &str, body: Value) -> Response {
        let request = self.request(Method::PUT, path).json(&body);
        self.send(request).await
    }

    pub async fn post_multipart(&mut self, path: &str, form: reqwest::multipart::Form) -> Response {
        let request = self.request(Method::POST, path).multipart(form);
        self.send(request).await
    }

    /// The signed-in user (`GET /auth/whoami`).
    pub async fn who_am_i(&mut self) -> Value {
        self.get("/auth/whoami").await.ok().json()["user"].clone()
    }
}

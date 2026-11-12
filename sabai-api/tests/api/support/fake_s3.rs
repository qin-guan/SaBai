//! An in-memory stand-in for the S3 bucket: create bucket, put, get and delete objects.

use std::collections::HashMap;
use std::net::SocketAddr;
use std::sync::{Arc, Mutex};

use axum::Router;
use axum::body::Bytes;
use axum::extract::{Path, State};
use axum::http::{HeaderMap, StatusCode, header};
use axum::response::{IntoResponse, Response};
use axum::routing::put;

type Objects = Arc<Mutex<HashMap<String, (Bytes, String)>>>;

pub async fn start() -> anyhow::Result<SocketAddr> {
    let objects: Objects = Arc::default();
    let app = Router::new()
        .route("/{bucket}", put(|| async { StatusCode::OK }))
        .route("/{bucket}/", put(|| async { StatusCode::OK }))
        .route("/{bucket}/{*key}", put(put_object).get(get_object).delete(delete_object))
        .with_state(objects);
    let listener = tokio::net::TcpListener::bind("127.0.0.1:0").await?;
    let address = listener.local_addr()?;
    tokio::spawn(async move {
        axum::serve(listener, app).await.unwrap();
    });
    Ok(address)
}

async fn put_object(
    State(objects): State<Objects>,
    Path((_, key)): Path<(String, String)>,
    headers: HeaderMap,
    body: Bytes,
) -> StatusCode {
    let content_type = headers
        .get(header::CONTENT_TYPE)
        .and_then(|value| value.to_str().ok())
        .unwrap_or("application/octet-stream")
        .to_owned();
    objects.lock().unwrap().insert(key, (body, content_type));
    StatusCode::OK
}

async fn get_object(State(objects): State<Objects>, Path((_, key)): Path<(String, String)>) -> Response {
    match objects.lock().unwrap().get(&key).cloned() {
        Some((body, content_type)) => ([(header::CONTENT_TYPE, content_type)], body).into_response(),
        None => (StatusCode::NOT_FOUND, "<Error><Code>NoSuchKey</Code></Error>").into_response(),
    }
}

async fn delete_object(State(objects): State<Objects>, Path((_, key)): Path<(String, String)>) -> StatusCode {
    objects.lock().unwrap().remove(&key);
    StatusCode::NO_CONTENT
}

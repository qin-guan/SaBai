#![allow(dead_code, unused_imports)]
//! Test support: the shared server and a small HTTP client.

pub mod client;
pub mod fake_s3;
pub mod setup;

use std::net::SocketAddr;
use std::sync::OnceLock;

use sabai_api::config::{Config, Settings};

pub use client::{Client, Response};
#[allow(unused_imports)]
pub use setup::*;

pub const ADMIN: &str = "admin@sabai.test";
pub const DOCTOR: &str = "doctor@sabai.test";
pub const PHARMACIST: &str = "pharmacist@sabai.test";
pub const REGISTRATION: &str = "registration@sabai.test";
pub const PASSWORD: &str = "Password1!";

pub struct TestApp {
    pub address: SocketAddr,
    pub database_url: String,
}

static APP: OnceLock<TestApp> = OnceLock::new();

/// The shared server, started on first use on a thread of its own so it outlives the
/// runtime of whichever test asked first.
pub fn app() -> &'static TestApp {
    APP.get_or_init(|| {
        let (sender, receiver) = std::sync::mpsc::channel();
        std::thread::spawn(move || {
            let runtime = tokio::runtime::Builder::new_multi_thread().enable_all().build().unwrap();
            runtime.block_on(async move {
                let started = start().await;
                let failed = started.is_err();
                sender.send(started).unwrap();
                if !failed {
                    std::future::pending::<()>().await;
                }
            });
        });
        receiver.recv().unwrap().expect("the test server failed to start")
    })
}

async fn start() -> anyhow::Result<TestApp> {
    let admin_url = std::env::var("SABAI_TEST_POSTGRES")
        .unwrap_or_else(|_| "postgres://postgres@localhost:5432/postgres".to_owned());
    let database = format!("sabai_test_{}", uuid::Uuid::new_v4().simple());
    let admin = sqlx::PgPool::connect(&admin_url).await?;
    sqlx::query(&format!("CREATE DATABASE \"{database}\"")).execute(&admin).await?;
    admin.close().await;
    let (base, _) = admin_url.rsplit_once('/').unwrap();
    let database_url = format!("{base}/{database}");

    let storage = fake_s3::start().await?;

    // The same configuration the previous test fixture used: demo staff, and sessions
    // re-validated on every request so lock/role/password changes are seen immediately.
    let config = Config::from_sources(
        [
            ("ASPNETCORE_ENVIRONMENT", "Development".to_owned()),
            ("ConnectionStrings__sabai", database_url.clone()),
            (
                "ConnectionStrings__fs-sabai",
                format!("Endpoint=http://{storage};AccessKey=test;SecretKey=test;Bucket=sabai"),
            ),
            ("Auth__SeedDemoUsers", "true".to_owned()),
            ("Auth__SecurityStampValidationMinutes", "0".to_owned()),
            ("FaceRecognition__AutoDownloadModels", "false".to_owned()),
        ]
        .map(|(key, value)| (key.to_owned(), value)),
        [],
    );
    let state = sabai_api::build_state(Settings::load(&config)?).await?;
    state.storage.ensure_bucket().await?;

    let listener = tokio::net::TcpListener::bind("127.0.0.1:0").await?;
    let address = listener.local_addr()?;
    let app = sabai_api::app(state);
    tokio::spawn(async move {
        axum::serve(listener, app.into_make_service_with_connect_info::<SocketAddr>()).await.unwrap();
    });
    Ok(TestApp { address, database_url })
}

/// Eight random hex characters, for names that must not collide between tests.
pub fn suffix() -> String {
    uuid::Uuid::new_v4().simple().to_string()[..8].to_owned()
}

/// A connection pool on the test database, for fixtures and checks that have no endpoint.
pub async fn db() -> sqlx::PgPool {
    sqlx::PgPool::connect(&app().database_url).await.expect("connecting to the test database")
}

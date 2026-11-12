use std::net::SocketAddr;

use anyhow::Context;
use sabai_api::config::{Config, Settings};
use tracing_subscriber::EnvFilter;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    tracing_subscriber::fmt()
        .with_env_filter(
            EnvFilter::try_from_default_env()
                .unwrap_or_else(|_| EnvFilter::new("info,tower_http=warn,sqlx=warn")),
        )
        .init();

    let settings = Settings::load(&Config::from_process())?;
    let listen = settings.listen.clone();
    let state = sabai_api::build_state(settings).await?;

    if state.storage.is_configured() {
        state.storage.ensure_bucket().await?;
    }

    let app = sabai_api::app(state);
    let mut servers = tokio::task::JoinSet::new();
    for address in listen {
        let listener = tokio::net::TcpListener::bind(&address)
            .await
            .with_context(|| format!("Could not listen on {address}."))?;
        tracing::info!("Now listening on: http://{address}");
        let app = app.clone();
        servers.spawn(async move {
            axum::serve(listener, app.into_make_service_with_connect_info::<SocketAddr>())
                .with_graceful_shutdown(shutdown_signal())
                .await
        });
    }
    while let Some(result) = servers.join_next().await {
        result??;
    }
    Ok(())
}

async fn shutdown_signal() {
    let interrupt = tokio::signal::ctrl_c();
    let mut terminate = tokio::signal::unix::signal(tokio::signal::unix::SignalKind::terminate())
        .expect("installing the SIGTERM handler");
    tokio::select! {
        _ = interrupt => {}
        _ = terminate.recv() => {}
    }
}

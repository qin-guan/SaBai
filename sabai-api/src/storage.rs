//! File storage in the RustFS (S3-compatible) bucket: patient documents, photos and
//! dental images. Path-style addressing, SigV4-signed requests.

use std::time::Duration;

use anyhow::{Context, anyhow, bail};
use axum::body::Bytes;
use futures::Stream;
use reqwest::StatusCode;
use rusty_s3::actions::{CreateBucket, DeleteObject, GetObject, PutObject, S3Action};
use rusty_s3::{Bucket, Credentials, UrlStyle};

use crate::config::StorageSettings;

const SIGNATURE_LIFETIME: Duration = Duration::from_secs(300);

#[derive(Clone)]
pub struct Storage {
    client: reqwest::Client,
    target: Option<Target>,
}

#[derive(Clone)]
struct Target {
    bucket: Bucket,
    credentials: Credentials,
}

/// A stored object being read back.
pub struct StoredObject {
    pub content_type: Option<String>,
    pub content_length: Option<u64>,
    response: reqwest::Response,
}

impl StoredObject {
    pub fn into_stream(self) -> impl Stream<Item = Result<Bytes, reqwest::Error>> + Send + 'static {
        self.response.bytes_stream()
    }

    pub async fn bytes(self) -> anyhow::Result<Bytes> {
        Ok(self.response.bytes().await?)
    }
}

impl Storage {
    pub fn new(settings: &StorageSettings) -> anyhow::Result<Self> {
        let client = reqwest::Client::builder().build()?;
        if !settings.is_configured() {
            return Ok(Storage { client, target: None });
        }
        if settings.access_key.trim().is_empty() || settings.secret_key.trim().is_empty() {
            bail!("RustFS connection string 'fs-sabai' is missing Endpoint, AccessKey, or SecretKey.");
        }
        let endpoint = settings.endpoint.parse().context("The storage endpoint is not a valid URL.")?;
        let region = if settings.region.trim().is_empty() { "us-east-1" } else { settings.region.as_str() };
        let bucket = Bucket::new(endpoint, UrlStyle::Path, settings.bucket.clone(), region.to_owned())
            .map_err(|error| anyhow!("The storage bucket is not valid: {error}"))?;
        let credentials = Credentials::new(settings.access_key.clone(), settings.secret_key.clone());
        Ok(Storage { client, target: Some(Target { bucket, credentials }) })
    }

    pub fn is_configured(&self) -> bool {
        self.target.is_some()
    }

    fn target(&self) -> anyhow::Result<&Target> {
        self.target
            .as_ref()
            .context("RustFS connection string 'fs-sabai' is missing Endpoint, AccessKey, or SecretKey.")
    }

    /// Creates the bucket if it does not exist. The store may still be starting, so this
    /// retries for a few seconds before giving up.
    pub async fn ensure_bucket(&self) -> anyhow::Result<()> {
        let target = self.target()?;
        let mut attempt = 1;
        loop {
            let url = CreateBucket::new(&target.bucket, &target.credentials).sign(SIGNATURE_LIFETIME);
            let outcome = match self.client.put(url).send().await {
                Ok(response) if response.status().is_success() => return Ok(()),
                Ok(response) => {
                    let status = response.status();
                    let body = response.text().await.unwrap_or_default();
                    if body.contains("BucketAlreadyOwnedByYou") || body.contains("BucketAlreadyExists") {
                        return Ok(());
                    }
                    anyhow!("creating the storage bucket failed: {status} {body}")
                }
                Err(error) => anyhow!(error),
            };
            if attempt >= 10 {
                return Err(outcome);
            }
            attempt += 1;
            tokio::time::sleep(Duration::from_secs(1)).await;
        }
    }

    pub async fn put(&self, key: &str, bytes: Bytes, content_type: &str) -> anyhow::Result<()> {
        let target = self.target()?;
        let url = PutObject::new(&target.bucket, Some(&target.credentials), key).sign(SIGNATURE_LIFETIME);
        let response =
            self.client.put(url).header(reqwest::header::CONTENT_TYPE, content_type).body(bytes).send().await?;
        if !response.status().is_success() {
            bail!("storing '{key}' failed: {}", response.status());
        }
        Ok(())
    }

    /// The object, or `None` when the bucket has no such key.
    pub async fn get(&self, key: &str) -> anyhow::Result<Option<StoredObject>> {
        let target = self.target()?;
        let url = GetObject::new(&target.bucket, Some(&target.credentials), key).sign(SIGNATURE_LIFETIME);
        let response = self.client.get(url).send().await?;
        if response.status() == StatusCode::NOT_FOUND {
            return Ok(None);
        }
        if !response.status().is_success() {
            bail!("reading '{key}' failed: {}", response.status());
        }
        let content_type = response
            .headers()
            .get(reqwest::header::CONTENT_TYPE)
            .and_then(|value| value.to_str().ok())
            .map(str::to_owned);
        Ok(Some(StoredObject { content_type, content_length: response.content_length(), response }))
    }

    /// Deletes the object. Deleting a key that does not exist succeeds.
    pub async fn delete(&self, key: &str) -> anyhow::Result<()> {
        let target = self.target()?;
        let url = DeleteObject::new(&target.bucket, Some(&target.credentials), key).sign(SIGNATURE_LIFETIME);
        let response = self.client.delete(url).send().await?;
        if !response.status().is_success() && response.status() != StatusCode::NOT_FOUND {
            bail!("deleting '{key}' failed: {}", response.status());
        }
        Ok(())
    }
}

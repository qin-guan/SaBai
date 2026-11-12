//! Shared application state handed to every handler.

use std::ops::Deref;
use std::sync::Arc;

use crate::auth::session::SessionKeys;
use crate::config::Settings;
use crate::db::Pool;
use crate::face::FaceService;
use crate::storage::Storage;

#[derive(Clone)]
pub struct AppState(Arc<Inner>);

pub struct Inner {
    pub settings: Settings,
    pub db: Pool,
    pub keys: SessionKeys,
    pub storage: Storage,
    pub faces: FaceService,
}

impl AppState {
    pub fn new(inner: Inner) -> Self {
        AppState(Arc::new(inner))
    }
}

impl Deref for AppState {
    type Target = Inner;

    fn deref(&self) -> &Inner {
        &self.0
    }
}

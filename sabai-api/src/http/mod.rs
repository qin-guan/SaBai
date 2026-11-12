//! HTTP plumbing shared by every endpoint: reading requests, validating them, and the
//! per-request context (session, audit actor, error bodies).

pub mod context;
pub mod lenient;
pub mod request;
pub mod response;
pub mod validate;

pub use context::Ctx;
pub use request::{Form, Req, UploadedFile};
pub use response::{Created, Json, NoContent, SetCookies};
pub use validate::{Rules, Validate};

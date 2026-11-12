//! Per-request context.
//!
//! One middleware wraps every request. On the way in it reads the session cookie and
//! works out who is acting (for authorization and the audit log); on the way out it
//! gives error responses their body (they need the request path) and refreshes the
//! session cookie when it slid or was re-validated.

use std::net::SocketAddr;

use axum::extract::{ConnectInfo, FromRequestParts, Request, State};
use axum::http::request::Parts;
use axum::http::{HeaderValue, header};
use axum::middleware::Next;
use axum::response::{IntoResponse, Response};
use rand::Rng;
use sqlx::PgConnection;

use crate::auth::session::{self, Principal, Ticket};
use crate::auth::users::{self, StaffUser};
use crate::db::{AuditActor, Pool, Tx};
use crate::error::{ApiError, ApiResult, Problem};
use crate::state::AppState;

#[derive(Debug, Clone)]
pub struct RequestContext {
    pub actor: AuditActor,
    pub principal: Option<Principal>,
    pub ticket: Option<Ticket>,
}

pub async fn layer(State(state): State<AppState>, mut request: Request, next: Next) -> Response {
    let path = request.uri().path().to_owned();
    let trace_id = new_trace_id();
    let ip = request.extensions().get::<ConnectInfo<SocketAddr>>().map(|info| info.0.ip());
    let mut actor = AuditActor::for_request(request.method().as_str(), &path, ip);

    let authentication =
        match session::authenticate(&state.keys, &state.settings.auth, &state.db, request.headers()).await {
            Ok(authentication) => authentication,
            Err(error) => return render(ApiError::from(error).into_response(), &path, &trace_id),
        };
    let principal = authentication.ticket.as_ref().map(Ticket::principal);
    if let Some(principal) = &principal {
        actor = actor.with_user(principal.id, &principal.user_name);
    }
    request.extensions_mut().insert(RequestContext { actor, principal, ticket: authentication.ticket });

    let mut response = render(next.run(request).await, &path, &trace_id);

    if let Some(cookie) = authentication.set_cookie {
        let prefix = format!("{}=", session::SESSION_COOKIE);
        let handler_set_it = response
            .headers()
            .get_all(header::SET_COOKIE)
            .iter()
            .any(|value| value.to_str().is_ok_and(|value| value.starts_with(&prefix)));
        if !handler_set_it {
            response.headers_mut().append(header::SET_COOKIE, cookie);
        }
    }
    if response.headers().contains_key(header::SET_COOKIE) {
        // Responses that carry a session must not be cached.
        let headers = response.headers_mut();
        headers.insert(header::CACHE_CONTROL, HeaderValue::from_static("no-cache,no-store"));
        headers.insert(header::PRAGMA, HeaderValue::from_static("no-cache"));
        headers.insert(header::EXPIRES, HeaderValue::from_static("Thu, 01 Jan 1970 00:00:00 GMT"));
    }
    response
}

fn render(mut response: Response, path: &str, trace_id: &str) -> Response {
    match response.extensions_mut().remove::<Problem>() {
        Some(problem) => {
            let mut rendered = problem.render(path, trace_id);
            for (name, value) in response.headers() {
                if name == header::SET_COOKIE {
                    rendered.headers_mut().append(name, value.clone());
                }
            }
            rendered
        }
        None => response,
    }
}

/// `0HNQ0LKHS8K92:00000001`-style id, to tell error reports apart.
fn new_trace_id() -> String {
    const ALPHABET: &[u8] = b"0123456789ABCDEFGHIJKLMNOPQRSTUV";
    let mut rng = rand::rng();
    let id: String = (0..13).map(|_| ALPHABET[rng.random_range(0..ALPHABET.len())] as char).collect();
    format!("{id}:00000001")
}

/// What a handler needs besides its request: the app state and who is acting.
///
/// ```ignore
/// async fn create(ctx: Ctx, _: Staff, Req(req): Req<Request>) -> ApiResult<Created<Response>> {
///     let mut tx = ctx.begin().await?;
///     let row = tx.insert(/* … */).await?;
///     tx.commit().await?;
///     // …
/// }
/// ```
#[derive(Clone)]
pub struct Ctx {
    pub state: AppState,
    pub actor: AuditActor,
    pub principal: Option<Principal>,
    pub ticket: Option<Ticket>,
}

impl Ctx {
    pub fn db(&self) -> &Pool {
        &self.state.db
    }

    /// Starts a transaction whose writes are audited as this request's user.
    pub async fn begin(&self) -> Result<Tx, sqlx::Error> {
        Tx::begin(&self.state.db, self.actor.clone()).await
    }

    /// The signed-in user's account, or 401 if the cookie points at a user that is gone.
    pub async fn current_user(&self, conn: &mut PgConnection) -> ApiResult<StaffUser> {
        let id = self.principal.as_ref().map(|principal| principal.id).ok_or(ApiError::AuthRejected)?;
        users::find_by_id(conn, id).await?.ok_or(ApiError::AuthRejected)
    }
}

impl FromRequestParts<AppState> for Ctx {
    type Rejection = ApiError;

    async fn from_request_parts(parts: &mut Parts, state: &AppState) -> Result<Self, ApiError> {
        let context = parts
            .extensions
            .get::<RequestContext>()
            .cloned()
            .ok_or_else(|| ApiError::internal("request context middleware is not installed"))?;
        Ok(Ctx {
            state: state.clone(),
            actor: context.actor,
            principal: context.principal,
            ticket: context.ticket,
        })
    }
}

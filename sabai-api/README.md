# sabai-api

The Sa'Bai clinic API in Rust: a one-to-one port of `SaBai.API` (ASP.NET Core). Same
routes, same JSON, same database schema, same configuration keys.

## Run

Needs Rust 1.88+ and Postgres. RustFS (S3) and Qdrant are needed for files and face
recognition; everything else works without them.

```bash
ConnectionStrings__sabai="Host=localhost;Username=postgres;Database=sabai" \
Auth__SeedDemoUsers=true ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5250 \
cargo run
```

The demo staff are `admin`, `doctor`, `pharmacist` and `registration`, password `Password1!`.
The API description is at `/openapi/v1.json`, a browsable reference at `/scalar`.

## Test

```bash
cargo test
```

Integration tests (`tests/api`) start the real application against a database they
create on a local Postgres (`SABAI_TEST_POSTGRES`, default
`postgres://postgres@localhost:5432/postgres`). Each run leaves a `sabai_test_*` database behind.

## Layout

| Path | What |
| --- | --- |
| `src/routes/` | HTTP endpoints, one module per area |
| `src/domain/` | Tables (one struct each) and the rules that belong to the data |
| `src/db/` | Audited transactions, entity mapping, migrations |
| `src/auth/` | Staff accounts, passwords, two-factor, cookie sessions |
| `src/http/` | Request binding, validation, per-request context |
| `src/face/`, `src/storage.rs` | Face recognition, the file bucket |
| `migrations/` | SQL migrations; `0001` is the schema EF Core produced |
| `openapi/v1.json` | The API contract, served as-is |

## Writing an endpoint

An endpoint is a handler function plus its request and response types, in the module of
its area. `src/routes/village_codes.rs` is the reference example.

```rust
/// Update a village code.
async fn update(ctx: Ctx, _: Staff, Req(req): Req<UpdateRequest>) -> ApiResult<Json<VillageCodeResponse>> {
    let mut tx = ctx.begin().await?;
    let before = find(&mut tx, req.id).await?;      // 404 with the API's message if missing
    let mut village = before.clone();
    // … apply the request …
    let village = tx.update(&before, village).await?; // writes changed columns + the audit entry
    tx.commit().await?;
    Ok(Json(village.into()))
}
```

- **Who may call it** is the second argument: `Staff` (admin or member) or `Admin`. Leave
  it out for public endpoints. It comes before `Req` so the session is checked first.
- **`Req<T>`** builds `T` from the JSON body, route parameters and query string together
  (the route wins over the body), reading leniently: any-case property names, numbers
  as strings, unknown properties ignored. Then it runs `T`'s `Validate` rules. Give
  fields that may be absent `Option` or `#[serde(default)]`; a field with neither is
  required. Use `Form` instead for `multipart/form-data`.
- **Validation** (`impl Validate`) reports every failing rule as
  `{"name": "userName", "reason": "'user Name' must not be empty."}`. Rules that depend
  on the database are checked in the handler and returned as `ApiError`.
- **Writes go through `Tx`**: `tx.insert(row)`, `tx.update(&before, after)`,
  `tx.delete(&row)`. Each writes the audit log entry in the same transaction, attributed
  to the request's user. Plain `UPDATE`/`DELETE` statements bypass the audit log, so
  load rows and change them through `Tx`. Reads are ordinary `sqlx` queries.
- **Errors** are `ApiError`: `not_found`, `conflict`, `bad_request`, `forbidden`,
  `locked`. Unique and foreign-key violations from the database become 409 by themselves.
- **Responses**: `Json(body)`, `Created::at(location, body)`, `NoContent`. Timestamps are
  `Ts`; enums are declared with `wire_enum!` (stored under the variant name, sent under
  the wire name).

When an endpoint is added or changed, update `openapi/v1.json` with it;
`tests/api/contract.rs` fails if a documented operation has no route.

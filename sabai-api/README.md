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
Face tests run the real models and skip themselves when the model files are not installed;
enrolled faces are kept in memory unless `SABAI_TEST_QDRANT` names a Qdrant gRPC endpoint.

## Layout

| Path | What |
| --- | --- |
| `src/routes/` | HTTP endpoints, one module per area |
| `src/domain/` | Tables (one struct each) and the rules that belong to the data |
| `src/db/` | Audited transactions, entity mapping, migrations |
| `src/auth/` | Staff accounts, passwords, two-factor, cookie sessions |
| `src/http/` | Request binding, validation, per-request context |
| `src/face/`, `src/storage.rs`, `src/pdf.rs` | Face recognition, the file bucket, the consult report |
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

## Coming from the .NET API

| .NET | Here |
| --- | --- |
| FastEndpoints slice (`Endpoint`/`Request`/`Response`/`Validator`) | A handler function with its request and response types in `src/routes/<area>` |
| FluentValidation validator | `impl Validate`, same messages |
| EF Core entity and `DbContext` | `entity!` struct in `src/domain`, `sqlx` queries |
| EF Core migrations | SQL files in `migrations/` |
| `SaveChanges` audit interceptor | `Tx::insert` / `update` / `delete` |
| ASP.NET Core Identity (`UserManager`, `SignInManager`) | `src/auth` (`users`, `signin`, `session`) on the same tables |
| `appsettings.json`, environment, arguments | `src/config.rs`, same keys (`Auth__SeedDemoUsers`, `--Auth:SeedDemoUsers=true`) |
| AWS SDK for S3 | Signed requests to the same bucket and keys (`src/storage.rs`) |
| Qdrant .NET client | `qdrant-client`, same collection and payload |
| ONNX Runtime + SkiaSharp | `ort` (the same runtime) + the `image` crate |
| QuestPDF | Typst, as a library |
| Generated OpenAPI document | `openapi/v1.json`, kept by hand |
| Aspire AppHost | `compose.yaml` |
| TUnit tests over an Aspire host | `tests/api`, over the real app and a real Postgres |

### Moving a deployment

Point this API at the same Postgres, bucket and Qdrant with the same environment
variables. On first start it adopts the EF Core schema (the database must be at the last
EF migration, `20261109033008_GeneralizeFilesAndAddDentalImage`) and creates its own
session key. Staff keep their passwords, authenticator apps and recovery codes. Everyone
signs in again once, because session cookies issued by the .NET API cannot be read.

### Known differences

Behaviour is the same wherever the previous test suite looks. These are the places it is not:

- **More forgiving requests.** Enum values are accepted in any case (`"pending"` for
  `"PENDING"`); one value is accepted where a list is expected; an empty body with a JSON
  content type is treated as no body; a timestamp with a non-UTC offset is converted
  (the .NET API answered 500 on some endpoints).
- **Wording of unreadable-request errors.** A malformed body or a value of the wrong type
  is still a 400 naming the same property, but the reason text is not System.Text.Json's.
  Validation messages and domain errors are identical.
- **Decimals** are written without trailing zeros (`36.6`, not `36.60`).
- **Timestamps** have microsecond precision everywhere; the .NET API showed seven
  fraction digits in the response to a create and six afterwards.
- **Order approval and stock edits lock the rows they change**, so two simultaneous
  approvals cannot both take the same stock. The .NET API had no such guard.
- **Consult PDF**: same content and layout, set in Lato with Noto Sans Khmer as the
  fallback, so line heights differ slightly from QuestPDF's output.
- **Sign-out** clears `sabai.auth` and the two-factor cookie; the .NET API also cleared
  the unused `Identity.External` cookie.
- **Face recognition** runs one inference per model at a time, rejects truncated image
  files that Skia would partly decode, and has no GPU provider in this build.

### Not ported

- **OpenTelemetry export** (traces, metrics and logs to `OTEL_EXPORTER_OTLP_ENDPOINT`).
  Logs go to standard output.
- **The Scalar page** loads its script from a CDN instead of serving it from the API.

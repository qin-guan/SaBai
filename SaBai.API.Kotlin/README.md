# SaBai.API.Kotlin: a Kotlin port, for comparison

A partial port of `SaBai.API` to **Kotlin + Spring Boot 4**. It exists so you can compare
the two stacks on real code. It is not a replacement. The parts it covers run against
the same Postgres schema as the C# API and read the same password hashes, so both can
point at one database.

## What's ported

| Area | Ported | Not ported |
| --- | --- | --- |
| Auth | login, two-factor login, logout, whoami; lockout; session re-validation (security stamp); seeding of roles, bootstrap admin and demo users | account self-service (enable 2FA, recovery codes, change password), user admin, "remember this browser" |
| Village codes | all 5 endpoints | |
| Patients | create (with visit + vitals), get, list, update, delete, enroll face, search by face | photo upload/download, visit journey, latest-visit list |
| Broadcasts | all 4 endpoints | |
| Consults | create (with diagnoses + orders), get, update (24h lock), PDF | list by visit, delete |
| Cross-cutting | audit log of every row change and sign-in, problem-details errors, validation, OpenAPI, health, OpenTelemetry | Aspire AppHost |
| Face recognition | the whole pipeline: SCRFD detection with best-effort retries, alignment, ArcFace embeddings, Qdrant store, model download | |

Not started: visits, vitals/eyesight/dental/puberty records, pharmacy stock and orders,
files, persons/faces admin, audit-log listing.

## Running it

It needs Postgres. Qdrant and RustFS are only needed for the face and photo endpoints.

```bash
./gradlew bootRun --args='--sabai.auth.seed-demo-users=true'
```

- API: http://localhost:8080. Docs: http://localhost:8080/scalar (Swagger UI).
- Settings are in `src/main/resources/application.yaml`. Override them with env vars
  (`SABAI_DB_URL`, `SABAI_DB_USER`, `SABAI_DB_PASSWORD`, `SABAI_QDRANT_HOST`, ...).
- Flyway creates the schema on an empty database. On a database the C# API already
  migrated, it baselines and only adds the session tables.

Tests:

```bash
./gradlew test
```

The integration tests boot the full app against Postgres through Testcontainers when
Docker is available. Without Docker, point them at a database you can drop:
`SABAI_TEST_DB_URL=jdbc:postgresql://localhost:5432/sabai_test`. The real-model face
test is opt-in, because the models are about 190 MB: set `SABAI_TEST_MODELS_DIR` and
`SABAI_TEST_FACES` (see `FaceModelTest`).

## How the pieces map

| C# | Kotlin |
| --- | --- |
| ASP.NET Core + FastEndpoints (`Endpoint<Request, Response>`) | Spring MVC: one package per endpoint, with `Request`, `Response` and `Endpoint` in one file |
| FluentValidation `Validator` | Bean Validation annotations on the `Request` (`@NotBlank`, `@Size`...), plus one custom `@NullOrNotBlank` |
| `Roles(Admin, Member)` / `AllowAnonymous()` | `@StaffOnly` / `@AdminOnly` meta-annotations; public routes listed in `SecurityConfig` |
| EF Core `DbContext`, LINQ | Hibernate (JPA) entities, Spring Data repositories, [Kotlin JDSL](https://github.com/line/kotlin-jdsl) for typed queries |
| EF migrations | Flyway SQL (`db/migration`) + Hibernate `ddl-auto: validate`, which fails startup if an entity and the schema disagree |
| `SaveChanges()` | none: changes to loaded entities flush when the `@Transactional` method returns |
| `SaveChangesInterceptor` (audit) | Hibernate post-insert/update/delete listeners (`data/AuditListener.kt`) |
| ASP.NET Identity | `auth/`: Identity-compatible PBKDF2 hasher, TOTP, lockout, security-stamp filter, over the same tables |
| Identity cookie + Data Protection keys in Postgres | Spring Session JDBC: the cookie holds a session id, and sessions live in Postgres |
| `async`/`await` + `CancellationToken` | plain blocking code on virtual threads (`spring.threads.virtual.enabled`) |
| `ProblemDetails` + exception handler | `ProblemDetail` + `@RestControllerAdvice`; each exception type carries its own status |
| Microsoft.ML.OnnxRuntime / SkiaSharp | `com.microsoft.onnxruntime` (same native runtime) / ImageIO + metadata-extractor |
| Qdrant.Client, AWSSDK.S3 | the official Java clients |
| QuestPDF | OpenPDF |
| Aspire ServiceDefaults | Actuator health + `spring-boot-starter-opentelemetry` |
| TUnit + Aspire.Hosting.Testing | JUnit 5 + `@SpringBootTest` + Testcontainers |

### Side by side: update a village code

C# (four files: `Endpoint.cs`, `Request.cs`, `Response.cs`, `Validator.cs`):

```csharp
public override async Task HandleAsync(Request req, CancellationToken ct)
{
    var village = await db.VillageCodes.FirstOrNotFoundAsync(x => x.Id == req.Id, "Village code was not found.", ct);
    if (req.Code is not null)
    {
        var code = req.Code.Trim();
        if (await db.VillageCodes.AnyAsync(x => x.Id != village.Id && x.Code == code, ct))
            throw new ConflictException("A village code with this code already exists.");
        village.Code = code;
    }
    if (req.Name is not null) village.Name = req.Name.Trim();
    // ...
    await db.SaveChangesAsync(ct);
    await Send.OkAsync(new Response(village.Id, village.Code, /* ... */), ct);
}
```

Kotlin (one file: `villagecodes/update/Endpoint.kt`):

```kotlin
data class Request(
    @NullOrNotBlank @Size(max = 255) val code: String? = null,
    @NullOrNotBlank @Size(max = 255) val name: String? = null,
    @Pattern(regexp = COLOR_HEX_PATTERN) val colorHex: String? = null,
    val isVisible: Boolean? = null,
)

@StaffOnly
@Transactional
@PatchMapping("/village-codes/{id}")
fun handle(@PathVariable id: Int, @Valid @RequestBody req: Request): VillageCodeResponse {
    val village = villageCodes.findOrNotFound(id, "Village code was not found.")
    req.code?.trim()?.let { code ->
        if (villageCodes.existsByCodeAndIdNot(code, village.id)) throw ConflictException("A village code with this code already exists.")
        village.code = code
    }
    req.name?.let { village.name = it.trim() }
    req.colorHex?.let { village.colorHex = it.trim() }
    req.isVisible?.let { village.isVisible = it }
    village.updatedAt = OffsetDateTime.now(clock)
    return village.toResponse()
}
```

## What the port showed

**Better than expected**
- **Less code.** The ported endpoints come to about 40% of the C# line count (900 vs
  2,100 lines, not counting blank lines and imports; the C# count includes its `///`
  doc comments). The face pipeline comes to about 63%. Some of that is language: data classes, `?.let`
  for partial updates, one file per endpoint. Some is design I could change in either
  language: one shared `VillageCodeResponse`/`PatientResponse` instead of identical
  records per endpoint, and one `PatientReader` instead of the same projection three
  times.
- **No async colouring.** Virtual threads make blocking JDBC, Qdrant and S3 calls cheap,
  so there's no `Async` suffix, `await` or `CancellationToken` anywhere.
- **Sealed types.** `SignInResult` is sealed, so the compiler checks that
  `SignInResponse.from` handles every outcome. Identity's `SignInResult` is a bag of
  booleans.
- **Kotlin JDSL reads close to LINQ.** `whereAnd(name?.let { ... }, code?.let { ... })`
  drops null filters, which covers the C# `if (...) query = query.Where(...)` chains.
- **Simpler audit logging.** Hibernate fires its events after each statement, so
  generated ids are already known. The C# interceptor's second `SaveChanges` pass for
  new rows isn't needed.
- **Schema checks at startup.** `ddl-auto: validate` caught a table missing from my
  migration on the first run.

**Worse or missing**
- **Migrations are hand-written.** There's no `dotnet ef migrations add`: Flyway runs
  SQL you write yourself.
- **No QuestPDF.** OpenPDF needs a hand-drawn footer and a template trick for
  "page X of Y" (`ConsultPdf.kt`).
- **No Identity equivalent.** Spring Security covers sessions and authorization, but
  password hashing, TOTP, lockout and the security stamp are about 300 lines in `auth/`.
- **No Aspire.** Local orchestration would be Docker Compose, and the dashboard would
  be any OTLP collector.
- **JPA traps.** Lazy loading outside a transaction fails, entity classes must be `open`
  (the `allopen` plugin does this), and `Order` is a JPQL keyword, so that entity is
  named `MedicationOrder`.
- **Kotlin warts.** Nullable request fields need `!!` after validation
  (`patients/create`), and Jackson 3's `JsonNode` has rough edges in tests.
- **Slower startup and more memory.** About 11 s to start and more RAM than the .NET
  API. GraalVM native-image would fix this, at the cost of build complexity.

**Things to know before switching**
- Enum constants use the C# names (`Male`, `EarNoseThroat`) because those are the values
  in the database. `@JsonProperty` gives the wire names.
- Bean names are fully qualified, because every endpoint class is named `Endpoint`.
- OpenAPI schemas use fully qualified names, so a regenerated `SaBai.Sdk` would get
  different type and operation names.
- The two-factor step ignores its own `rememberMe` and uses the choice from the password
  step. "Remember this browser" isn't implemented.

## Verified

- 27 tests pass, plus 2 opt-in tests that need the real face models (they also pass
  when the models are supplied). Unit tests cover the hasher (against hashes in Identity's byte layout,
  generated independently), RFC 6238 vectors, domain rules and face geometry.
  Integration tests run the full app on Postgres and cover login, lockout, two-factor,
  village codes, patients, the audit log and the consult lock.
- With the real InsightFace models, two different portraits of the same person score a
  cosine similarity of 0.77 (match threshold 0.4).
- Not verified: the Qdrant store and the S3 calls (no Qdrant or S3 in the sandbox),
  rejection of a *different* person by the real models, and running the C# and Kotlin
  APIs side by side on one live database.

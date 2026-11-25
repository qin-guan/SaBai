# SaBai.Import

`sabai-import` backfills a SaBai database from a running deployment of the reference
implementation, [NUS-Project-SaBai/project-sabai](https://github.com/NUS-Project-SaBai/project-sabai)
(Next.js + tRPC + Drizzle/Supabase). It reads upstream **only** through upstream's tRPC *query*
procedures over HTTP GET, then copies patient photos from Cloudinary, enrols faces in SaBai's
face search (Qdrant), and copies patient files from upstream's S3 links.

**Nothing is ever changed upstream.** See [Read-only guarantees](#read-only-guarantees).

It writes into SaBai through the API's own code (`SaBai.API` is referenced, not copied): EF Core
entities and `AppDbContext` (with the API's audit interceptor), the RustFS client, `FaceAnalyzer`
and `FaceStore`. Imported rows look like rows created through the API.

## Usage

```sh
export SABAI_IMPORT_UPSTREAM_URL=https://sabai.example.org     # the upstream site
export SABAI_IMPORT_UPSTREAM_COOKIE='sb-abcd-auth-token=base64-...'  # see "Upstream credential"

# Target: the same settings the API uses
export ConnectionStrings__sabai='Host=...;Database=sabai;Username=...;Password=...'
export ConnectionStrings__fs-sabai='Endpoint=http://rustfs:9000;AccessKey=...;SecretKey=...;Bucket=sabai'
export ConnectionStrings__qdrant='Endpoint=http://qdrant:6334;Key=...'

dotnet run --project SaBai.Import -- data --dry-run   # fetch + map + check, write nothing
dotnet run --project SaBai.Import -- data             # phase 1: records
dotnet run --project SaBai.Import -- media            # phase 2: photos, faces, files
dotnet run --project SaBai.Import -- all              # both
```

| Option | Meaning |
| --- | --- |
| `--upstream-url <url>` | Upstream site (or `SABAI_IMPORT_UPSTREAM_URL`). |
| `--cookie-file <path>` | Read the cookie from a file instead of `SABAI_IMPORT_UPSTREAM_COOKIE`. The file is re-read when the session nears expiry, so a long run can be kept going by pasting a fresh cookie into it. |
| `--min-token-minutes <n>` | Stop once the session has less than *n* minutes left (default 5, minimum 3). |
| `--concurrency <n>` | Upstream/media requests in flight at once (default 4). |
| `--retries <n>` | Attempts per GET on network errors, HTTP 5xx or 429, with exponential backoff (default 4). |
| `--dry-run` | Fetch everything, map and check it against the SaBai database, write nothing. In `media` it downloads and validates photos and files but stores nothing. |
| `--record-upstream-face-consent` | `media`: see [Faces and consent](#faces-and-consent). |
| `--skip-files` | `media`: do not copy patient files. |
| `--cloudinary-url-prefix <p>` | `media`: build photo URLs as `<p>/<patientImagePublicId>` instead of using upstream's `patientImageUrl` (or `SABAI_IMPORT_CLOUDINARY_URL_PREFIX`). Only needed if upstream's `CLOUDINARY_URL_PREFIX` is wrong. |
| `--ConnectionStrings:sabai=...` etc. | Any .NET configuration key, as for the API. |

The database must already be migrated (start the SaBai API once); the tool does not migrate.
Secrets (cookie, connection strings) are never printed. Run it while the SaBai API is idle,
ideally before the clinic starts using SaBai.

Each phase prints a table per entity: **fetched / created / updated / skipped / failed**, then
notes (judgment calls applied, such as renumbered patients) and every failure with its upstream id.
Exit code: 0 all good, 2 some rows failed (the rest were imported), 1 stopped (credential,
read-only guard, upstream error), 64 bad arguments.

### Re-running

Both phases are idempotent and resumable. Every imported row's id is derived from its upstream
table and id (`Importing/DeterministicIds.cs`), so a second run finds its rows: unchanged ones are *skipped*, changed ones *updated*, nothing is duplicated, and
foreign keys are computed without a mapping table. The media phase skips patients whose photo /
face / file is already there. A failed row is reported and the run continues; its children are
reported as failed too ("its visit 123 was not imported"). Fix and re-run.

The derived ids have SaBai's UUIDv7 layout, because many SaBai lists order by id as creation order
(broadcast paging with `since`, the audit log, tie-breaks). Their 48-bit timestamp is
2020-01-01T00:00:00Z plus the upstream serial id in milliseconds, and the remaining bits come from
SHA-256 of `table:id`. So imported rows keep upstream's creation order among themselves and sort
before everything created in SaBai; nothing upstream can edit (dates, names) goes into an id, so
an id never changes between runs. Tables keyed by UUID upstream (referral partners, users) use the
epoch itself as timestamp.

## Upstream credential

Upstream authenticates tRPC calls with Supabase Auth session **cookies**
(`src/server/context.ts`: `createServerClient` from `@supabase/ssr` reads the request cookies and
calls `supabase.auth.getUser()`; `src/server/trpc.ts`: `protectedProcedure` requires that user and a
linked `public.users` row). Signing in creates a session on Supabase, so the tool never signs in,
refreshes or signs out. The operator gives it a session they already have:

1. Sign in to the upstream site in a browser with an account linked to a `users` row (any role:
   every query used here is a plain `protectedProcedure`).
2. Open devtools → Application/Storage → Cookies → the upstream site, or copy the `Cookie` request
   header of any `/api/trpc/...` request in the Network tab.
3. Take every cookie named `sb-<project-ref>-auth-token` (large sessions are split into
   `sb-<ref>-auth-token.0`, `.1`, ...; take all parts) and put them, as `name=value; name=value`, in
   `SABAI_IMPORT_UPSTREAM_COOKIE` or in a file passed with `--cookie-file`. A whole copied `Cookie:`
   header is also accepted. Treat it like a password.

**Why the expiry check.** Upstream's server refreshes a session whose access token expires within
90 seconds (`@supabase/auth-js` `EXPIRY_MARGIN_MS`, used by both `src/proxy.ts` and
`src/server/context.ts`). A refresh rotates the session on Supabase, which is a write. The tool
therefore reads `expires_at` from the cookie, or falls back to the access token's JWT `exp` claim
(some browser cookies omit `expires_at`), and refuses to send any request once fewer than
`--min-token-minutes` (default 5) remain. It refuses a cookie if neither expiry is readable.
Supabase access tokens last 1 hour by default. If a run stops for this reason, reload the upstream
app in the browser (the browser refreshes its own session), copy the cookie again and re-run, or
paste it into the `--cookie-file` while the run is going.

## Read-only guarantees

Enforced in code, not by convention:

1. **One upstream client.** `Upstream/UpstreamClient.cs` is the only code that talks to the
   upstream host. `Upstream/MediaDownloader.cs` (Cloudinary, S3) refuses the upstream host.
2. **Allowlist of queries.** The client only builds requests for the procedures below; anything
   else throws before a request exists. Each was checked in upstream's router source
   (`dev` at `a233797`) to be `protectedProcedure...query(...)` whose resolver only runs SELECTs:

   | Procedure | Input | Used for |
   | --- | --- | --- |
   | `villageCodesRouter.list` | `{ includeHidden: true }` | village codes (hidden ones too) |
   | `patientsRouter.list` | — | patients, photo URLs, Rekognition face id |
   | `visitsRouter.list` | — | visits |
   | `vitalsRouter.getByVisitId` | `{ visitId }` per visit | vitals |
   | `pubertyRouter.getByVitalId` | `{ vitalId }` per vitals | puberty |
   | `eyesightRouter.getByVisitId` | `{ visitId }` per visit | eyesight |
   | `consultsRouter.getByVisitId` | `{ visitId }` per visit | consults with their diagnoses |
   | `referralRouter.list` | — | referrals |
   | `referralPartnersRouter.list` | — | referral partners |
   | `medicationActiveIngredientsRouter.list` | — | active ingredients |
   | `medicationBrandRouter.list` | — | brands |
   | `medicationStockRouter.list` | — | stock (rows with quantity ≠ 0) |
   | `broadcastsRouter.list` | `{ since: 0 }` | every broadcast (without `since` it caps at 200) |
   | `filesRouter.listByPatientId` | `{ patientId }` per patient (media) | patient files |

   `filesRouter.listByPatientId` also creates presigned S3 GET links: that is a signature computed
   locally by the AWS SDK (`getSignedUrl`), with no AWS call and nothing stored. No query was
   excluded for side effects; unused queries (`getById`s, `visitsRouter.hasRecentVisit`,
   `listWithLatestVisit`, ...) are simply not on the list. `patientsRouter.searchPatientsByPicture`
   is a *mutation* (and calls Rekognition) and is never called.
3. **GET-only HTTP handler.** Every outside request goes through `ReadOnlyGuardHandler`, which
   throws for any method other than GET (and for a GET with a body), whatever the host. Redirects
   are not followed (a redirect to `/login` is reported as a rejected credential). Retries happen
   only inside these GETs.
4. **Server side.** tRPC v11 itself never runs a mutation for a GET (`resolveResponse.ts`:
   `mutation: ['POST']`), and upstream does not enable method override.
5. **No other access.** The tool never connects to upstream's Postgres/Supabase, never uses its
   service keys, never calls Cloudinary's Admin/Upload API or S3 APIs: only GETs of Cloudinary
   delivery URLs and presigned S3 links, without credentials. Photo delivery URLs need no signing:
   upstream uploads with the default `upload` type (`src/server/utils/cloudinary.ts`) and stores
   `image/upload/v<version>/<public_id>`; `patientImageUrl` is `${CLOUDINARY_URL_PREFIX}/<that>`.

Tests: `SaBai.Tests/Import/ReadOnlyGuardTests.cs` (non-GET refused, non-allowlisted procedures
refused with zero requests sent), `UpstreamCredentialTests.cs` (expiring session never sent), and
the integration test checks that the fake upstream saw only allowlisted GETs.

**Wire format** (checked against `@trpc/server` 11.18.0, the version in upstream's lockfile): a
non-batched GET `/api/trpc/<procedure>?input=<url-encoded JSON>`. Upstream configures the
`superjson` transformer, so the input is `{"json": <input>}` and results arrive as
`{"result":{"data":{"json": <value>, "meta": {...}}}}`; errors as
`{"error":{"json":{"message","code","data":{"code","httpStatus"}}}}`. Dates are ISO strings in
`json`; Postgres `numeric` columns are strings. Batching is not needed.

## Mapping

| Upstream table | Procedure | SaBai entity | Notes |
| --- | --- | --- | --- |
| `village_codes` | `villageCodesRouter.list` | `VillageCode` | If a village with the same code already exists here (made by hand), upstream rows are linked to it and it is left unchanged (noted). |
| `patients` | `patientsRouter.list` | `Patient` | `Number` = upstream id (see below). `VillageCodeId` = village of the latest upstream visit (what upstream shows beside the id). `CreatedAt` = first visit date. `ToGetReport` = false. Blank id/contact numbers become null, as in the API. |
| `visits` | `visitsRouter.list` | `Visit` | Status `started` (upstream has no visit status). |
| `vitals` | `vitalsRouter.getByVisitId` | `Vitals` | `RecordedAt` = visit date (upstream does not record it). |
| `puberty` | `pubertyRouter.getByVitalId` | `PubertyAssessment` | |
| `eyesight` | `eyesightRouter.getByVisitId` | `Eyesight` | `RecordedAt` = visit date. |
| `consults` | `consultsRouter.getByVisitId` | `Consult` | `DoctorId` → placeholder staff account (see Users). Upstream keeps referrals in their own table, so `Consult.ReferredFor/ReferralNotes` stay null. |
| `diagnosis` | (inside consults) | `Diagnosis` | Category by name; unknown text → `Others` with `(category: <text>)` appended to the details. |
| `referrals` | `referralRouter.list` | `Referral` | State by name. Booth/party/money fields stay empty (upstream has none). |
| `referral_partners` | `referralPartnersRouter.list` | `ReferralPartner` | |
| `medication_active_ingredients` | `medicationActiveIngredientsRouter.list` | `MedicationActiveIngredient` | |
| `medication_brands` | `medicationBrandRouter.list` | `MedicationBrand` | |
| `medication_stock` | `medicationStockRouter.list` | `MedicationStock` | See Pharmacy. |
| `broadcasts` | `broadcastsRouter.list` | `Broadcast` | `CreatedBy` (an email here) = upstream sender name. |
| `users` | — (ids only, via consults and broadcasts) | `StaffUser` placeholders | See Users. |
| `files` | `filesRouter.listByPatientId` (media) | `StoredFile` + RustFS object | Bytes downloaded from the presigned link, type checked from the bytes as `POST /files` does, stored as `<id>.<ext>`. |
| patient photo (Cloudinary) | `patientsRouter.list` → `patientImageUrl` (media) | `Patient.PhotoStoredFileName` + RustFS object | Checked like `PUT /patients/{id}/photo` (JPEG/PNG by bytes, ≤ 10 MB, decodes); key `patients/<patientId>/<derived id>.<ext>`. |
| Rekognition face | (photo, media) | Qdrant face + `Patient.LatestFaceId` | Embedded by `FaceAnalyzer` from the photo, as `POST /patients/{id}/faces`; Rekognition data itself cannot be moved. |

### Patient numbers

Upstream shows a patient as `villageCode + id` (padded to 4) on screens and cards; SaBai shows
`villageCode + Number`. So `Number` is the upstream id, and the printed ids keep working. If
another patient here already has that number, Postgres assigns a new one and the run notes
`upstream patient X ... imported as number Y`. Afterwards the `Patients.Number` identity sequence
is moved forward to the highest number, so patients registered through the API never collide with
imported ones. That is the one SQL statement the tool runs itself (EF Core has no API for
sequences): a constant `setval(...)` with no input, which only ever moves the sequence forward.
Everything else is LINQ and tracked entities.

### Users

Upstream has no query that returns its `users` table (`src/db/schema/users.ts`); records only carry
user ids (`consults.doctorId`, `broadcasts.createdBy`), plus `senderName` on broadcasts. Each
distinct upstream user becomes a **placeholder staff account**: user name `upstream-<uuid>`,
nickname = the broadcast sender name when known, otherwise `Upstream user <first 8 hex>`; no
password, no roles, permanently locked out (cannot sign in). An admin can rename these accounts.
Accounts are found by user name, so an existing `upstream-<uuid>` account (e.g. renamed by an
admin) is reused and never modified by a re-run.

Rows the import writes are attributed to an **`upstream-import`** staff account (same kind of
placeholder, also found by user name): the tool runs under a synthetic request context, so `AppDbContext` stamps
`UpdatedByUserId` with it and `AuditInterceptor` writes the usual audit entries with user name
`upstream-import` and request `IMPORT /upstream/data` (or `/media`). A re-run that changes nothing
writes no audit entries. `UpdatedAt` is the import time, as for any save.

### Pharmacy

Upstream's model: a stock row has a status (`active`, `discarded`, `donated`, `reserved`,
`dispensed`); placing an order (by active ingredient) splits stock rows and flips the reserved
rows to `reserved`, recorded in `order_items`. SaBai's model: a batch has `Quantity` and
`ReservedQuantity` (held by `OrderAllocation`s of pending orders), statuses active / discarded /
donated only.

**Upstream has no query for orders or order items** (`ordersRouter` only has the `create`
mutation), so no orders can be imported, and therefore no reservations either: every imported
batch has `ReservedQuantity = 0`, which keeps the check constraints and invariants trivially true.
Mapping of stock rows:

| Upstream status | Imported as |
| --- | --- |
| `active` | active batch, same quantity |
| `discarded`, `donated` | batch with that status |
| `reserved` | **active** batch with its quantity, remark `[Imported: reserved upstream for an order that could not be imported]` (judgment call: the units are still on the shelf; their order is lost, so pharmacists re-enter pending orders here and they reserve normally) |
| `dispensed` | **not imported** (skipped, noted): the units have left the shelf; here dispensing takes units off the batch |

New batches go through `MedicationStock.Create` with the same `StockChangeLog.ForCreate` rows the
API writes (by `upstream-import`); on a re-run changes go through `AdjustQuantity` / `ChangeStatus`
(which refuse to break a reservation made here since) with `StockChangeLog.ForUpdate` rows.
`medicationStockRouter.list` leaves out rows with quantity 0 (parents of splits), so split lineage
(`ParentStockId`) is not reconstructed, and a batch that dropped to 0 upstream after an earlier
import is not updated.

### Faces and consent

SaBai only enrols a face with the patient's current biometric consent (`BiometricConsent`); the
media phase checks it the same way. Upstream has no consent records. By default, faces are
enrolled only for patients who already have consent recorded in SaBai; the rest are skipped
(counted, and enrolled by a later `media` run once consent is recorded). With
`--record-upstream-face-consent`, for each patient that upstream had enrolled in face search
(`rekognitionFaceId` set) and that yields a usable face, the tool records a
`BiometricFaceRecognition` consent attributed to `upstream-import`, with a note saying it was
derived from upstream's enrolment and is not a consent given in SaBai. Whether that is acceptable
is the clinic's decision. A consent recorded this way and revoked later is respected on re-runs.
A photo with no detectable face is reported as a failure; the photo itself is still stored.

## Known gaps (not importable through upstream's queries)

- **Orders and order items** (`orders`, `order_items`): no query route. Pending upstream orders and
  their reservations, and the dispensing history, are not imported (see Pharmacy).
- **Stock change history** (`stock_changes`): no query route.
- **Staff users** (`users`): no query route; placeholders only (see Users). Auth accounts are
  Supabase's and are not importable at all.
- **Child vitals** (`child_vitals`: scoliosis, pallor, gross motor, ...): no query route
  (`vitalsRouter` does not return them).
- **Dental** (`dental`, `dental_diagnosis`): no router.
- **Soft-deleted files** (`files.is_deleted = true`): `listByPatientId` leaves them out.
- **Stock rows with quantity 0** (split parents) and **dispensed** stock: see Pharmacy.
- **Broadcast `createdBy` as an account link**: stored as the sender name (SaBai keeps a string).
- **Rekognition faceprints**: cannot be moved; faces are re-enrolled from the photos.
- Patients whose files' download links fail (e.g. upstream S3 not configured) are reported per
  patient.

## Tests

`SaBai.Tests/Import/`: guard and credential unit tests, mapping tests from recorded-shape fixtures
(`Fixtures/`), and `ImportIntegrationTests`, which run both phases twice against a fake tRPC
server and a fake image/file host into the real test stack (Postgres, RustFS, Qdrant) and check
the second run changes nothing. The face model finds no face in the generated test images, so that
test uses a fixed embedding stored in the real Qdrant.

```sh
dotnet test --project SaBai.Tests/SaBai.Tests.csproj -- --treenode-filter "/*/SaBai.Tests.Import/*/*"
```

Nothing here has been run against a real upstream deployment: the wire format and data shapes are
taken from upstream's source and the `@trpc/server` / `@supabase/*` package sources. Try
`data --dry-run` first.

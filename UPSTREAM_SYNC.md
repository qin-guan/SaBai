# Upstream sync

SaBai is a port of [NUS-Project-SaBai/project-sabai](https://github.com/NUS-Project-SaBai/project-sabai).
Merges to upstream `dev` are ported here daily.

Last synced upstream commit: `841e3f4`

## Mapping upstream changes

| Upstream                          | Here                                                                 |
| --------------------------------- | -------------------------------------------------------------------- |
| tRPC router procedure             | FastEndpoints slice in `SaBai.API/Endpoints/<Area>/<Action>/<Verb>/` |
| `trpc.*.useQuery/useMutation`     | `@sabai/sdk` client + TanStack Query (regenerate with `pnpm generate` in `SaBai.Sdk`) |
| Drizzle schema / Supabase migration | EF Core entity in `SaBai.API/Domain` + migration in `SaBai.API/Data/Migrations` |
| Supabase storage                  | RustFS (S3)                                                          |
| Face search (Supabase/pgvector)   | Qdrant                                                               |
| Supabase auth / RLS policies      | ASP.NET Core Identity (cookie sessions) + `Roles(...)` on each endpoint |
| Root `src/`, `package.json`       | `SaBai.Web/`                                                         |

## Notes

- Upstream #303 (Supabase Realtime `SELECT` policy for the `notifications` topic) has no
  equivalent here: there is no Realtime channel, and broadcast endpoints already require an
  authenticated staff role.
- Upstream #278 changed `.husky/pre-commit` to run `lint-staged`; only the `lint-staged`
  config was ported because this repo has no husky hook.
- Upstream #243 logs stock changes with a Postgres trigger. Here the same rows are written in
  code via `Domain/StockChangeLog.cs`, in the same `SaveChanges` as the stock edit, so the
  current staff user is recorded without session variables. Dispensing an order writes its own
  quantity rows, tagged with the order (`StockChange.OrderId`), so medication history lists them
  once, as the order (upstream has no equivalent flow yet).
- Upstream #286 (friendly duplicate village code error) was already covered: the API returns
  409 with a message and the modal shows it.
- Upstream #307 (`pnpm install --frozen-lockfile` in CI) was already covered by
  `SaBai.Web/Dockerfile`; no CI workflow here installs pnpm packages.
- Upstream #287 adds patient files on S3 with presigned upload/view URLs (to avoid Vercel's
  4.5MB request cap). Files here already exist and stream through the API to RustFS, so only
  upstream's upload rules were ported: PDF/JPEG/PNG/DOCX only, non-empty, up to 25MB.
- Upstream #327 and #332 add dental tables (`dental`, `dental_diagnosis`). They overlap
  `DentalConsult` here (qin-guan/SaBai#7), which already covers more (diagnoses on the record,
  treatment dates, referral) and models a few fields differently, so the model was kept as is.
  Upstream's `image` column became `DentalConsult.ImageFileId`, a foreign key to the shared
  `Files` table (`StoredFile`, formerly `ClinicalFile`), with the bytes in RustFS like every
  other file. Upload with `POST /files`, then link it by id in `PUT /visits/{id}/dental`.
- Also ported from that range, so don't redo them: #318 (zod, already newer here), #328
  (spacing and breadcrumbs), #330 (stock page buttons), #311 (broadcast list `limit`/`since`).
  `pnpm generate` works again: `@kubb/plugin-fetch` is 5.13.0, the release that goes with kubb
  5.16.0 (5.5.6 predated its plugin API). Set `SABAI_OPENAPI_URL` to generate from an API on
  another port.
- Upstream #336 (undici bump) only changes upstream's lockfile for a transitive dependency. This
  repo's lockfile resolves undici on its own, so there is nothing to port.
- Upstream #308 (`publishBroadcast` helper posting to Supabase Realtime) has no equivalent here:
  there is no Realtime channel.
- Upstream #340 (Khmer font) is already covered for `SaBai.Web.Nuxt` by qin-guan/SaBai#27 (Noto Sans
  Khmer in `main.css`, and Khmer fonts for the consult PDF). It is not applied to `SaBai.Web`.
- Upstream #301 (referrals frontend) and #256 (referral creation in consult form): The referral UI
  (referral list page at `/referrals`, detail page at `/referrals/:id`, `ReferralStateDropdown` component,
  and "Referrals" navigation item) and referral creation in the doctor's consult form are fully implemented
  in `SaBai.Web.Nuxt`. In addition, consult PDF generation and download (upstream #47) are integrated across
  the consult form, previous consults cards, and referral pages.
- **Offline use & UUIDv7 PKs**: All clinic domain entities in `SaBai.API` use `Guid` (UUIDv7) primary and
  foreign keys instead of `int` identity keys (while keeping `Patient.Number` as a Postgres identity column for
  human-readable card IDs). The REST API is the only read and write path. `SaBai.Web.Nuxt` keeps fetched data
  in a persisted query cache and queues writes made offline, replaying them later. To support that, create
  endpoints accept an optional client-chosen `id` and return the existing record when the same create is sent
  again, and a write that carries `If-Unmodified-Since` gets `412 Precondition Failed` when it would change a
  record another user changed after that time (`ITrackedEntity` and `AppDbContext`).
- Upstream #329 (stock edits): ported as API rules, on top of this repo's reservation model (see
  #312 below). A batch's reservations are a quantity (`ReservedQuantity`), not a status, so the
  statuses are only active, discarded and donated. While a batch holds reservations: an adjustment
  can't take its quantity below what is reserved, its status can't leave active, and a split moves
  only its unreserved units (409 for each).
- Upstream #339 (medication brands): its UI part is ported in `SaBai.Web`. Its API change makes
  `remarks` optional and stores blank remarks as null. That was not ported: the brand API
  deliberately requires `remarks` ("may be an empty string") and changing it alters the contract.
- Upstream #346 (broadcasts send): only the list fix was ported: with `since`, every newer
  broadcast is returned and `limit` is ignored (`limit` still caps the list without `since`).
  Not ported: the Supabase Realtime outbox publish, the 5000-character body cap (it exists to stay
  under Realtime's payload limit) and `senderName` (needs the held `users` table, #342).
- Upstream #368 (source-map-js) is a transitive lockfile bump for upstream's Next app: nothing to port.
- Decisions (owner): upstream UI changes are ported into `SaBai.Web` (Next.js) one by one on request;
  `SaBai.Web.Nuxt` is not touched by the daily sync; on request a gap pass ports what it lacks (last one: below). Ordering follows
  upstream #312: orders are for an active ingredient (no brand), stock is reserved when the order is
  placed (earliest expiry first, across brands), cancelling or deleting releases it, the first order
  wins. Beyond upstream: a pending order's quantity and ingredient can be changed (re-allocated),
  and dispensing is status `DISPENSED` (was `APPROVED`). The model is `MedicationStock.Quantity` /
  `ReservedQuantity` with an `xmin` concurrency token, `OrderAllocation` rows, and
  `Data/OrderLifecycle.cs` for every step. Upstream #342 (`users` table) is skipped.
- Ported into `SaBai.Web` from the held range: #301 (referral pages), #285 (toast inside modals),
  #338 (splitting modal), #339 (medication brands page), #367 (remark overflow), #262 (visit
  summary panel), #358 and #360. #335 and #337 (dental pages) were reviewed and skipped: this repo's
  dental UI already does more.
- #312 frontends: `SaBai.Web` has upstream's ingredient ordering form (Add Order modal in the
  consult form) and reserved/available on the stock page; its orders page is a placeholder, as
  upstream's is. `SaBai.Web.Nuxt` has the full flow: ordering by ingredient in the consult form,
  change/cancel of pending orders in previous consults, and the pharmacist queue with each order's
  allocations to pick from, dispense, change and cancel.
- Reviewed through `841e3f4`. Later upstream merges are for the next run.
- Nuxt gap pass (reviewed through upstream `a233797`): every upstream change since the sync began
  was checked against `SaBai.Web.Nuxt`. Ported there: #367 (remarks wrap; multiline edit field in
  brand and ingredient rows; referral partners wrap; main's reworked stock rows already clamp
  remarks), #262 (visit summary grouped as anthropometric, cardiovascular and
  metabolic with urinalysis and clinical remarks; diabetes "Yes" in red; summary stays in view
  beside the consult form). Left alone: #376 (patient photo standardisation; Nuxt's compact list
  thumbnails are deliberate, owner to decide) and #285 (toasts inside modals; unverified, Nuxt has
  one global toaster). #338 was dropped: the reworked split dialog no longer shows a remarks field for the parent.
  Everything else was already present.

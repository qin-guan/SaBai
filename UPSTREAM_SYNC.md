# Upstream sync

SaBai is a port of [NUS-Project-SaBai/project-sabai](https://github.com/NUS-Project-SaBai/project-sabai).
Merges to upstream `dev` are ported here daily.

Last synced upstream commit: `e16af5fbb6f100d32e78377367115e75bca61288`

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
  current staff user is recorded without session variables. Order approval keeps its own
  quantity rows (upstream has no equivalent flow yet).
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
  (spacing and breadcrumbs), #330 (stock page buttons), #311 (broadcast list `limit`/`since`;
  the SDK was not regenerated for it because `pnpm generate` fails on main, kubb core 5.16.0 with
  plugin-fetch 5.5.6; the new query params are optional and unused by the web app).
- Upstream #336 (undici bump) only changes upstream's lockfile for a transitive dependency. This
  repo's lockfile resolves undici on its own, so there is nothing to port.

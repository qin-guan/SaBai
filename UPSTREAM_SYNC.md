# Upstream sync

SaBai is a port of [NUS-Project-SaBai/project-sabai](https://github.com/NUS-Project-SaBai/project-sabai).
Merges to upstream `dev` are ported here daily.

Last synced upstream commit: `1559e9b9e0c1ad60426ae0e461a1f5b9b80b0c9b`

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
- Held for a decision (so the synced commit stays at `1559e9b`): upstream #327 and #332 add dental
  tables, which overlap `DentalConsult` here (from qin-guan/SaBai#7). Already ported from the
  same range, so don't redo them: #318 (zod, already newer here), #328 (spacing and
  breadcrumbs), #330 (stock page buttons).
- Already ported from the held range, so don't redo it: #311 (broadcast list `limit`/`since`).
  The SDK was not regenerated for it because `pnpm generate` currently fails on main (kubb
  core 5.16.0 with plugin-fetch 5.5.6); the new query params are optional and unused by the web app.

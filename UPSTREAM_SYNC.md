# Upstream sync

SaBai is a port of [NUS-Project-SaBai/project-sabai](https://github.com/NUS-Project-SaBai/project-sabai).
Merges to upstream `dev` are ported here daily.

Last synced upstream commit: `703cc08f7bd6aa52f9a39aa60f2d5bae41b65cfc`

## Mapping upstream changes

| Upstream                          | Here                                                                 |
| --------------------------------- | -------------------------------------------------------------------- |
| tRPC router procedure             | FastEndpoints slice in `SaBai.API/Endpoints/<Area>/<Action>/<Verb>/` |
| `trpc.*.useQuery/useMutation`     | `@sabai/sdk` client + TanStack Query (regenerate with `pnpm generate` in `SaBai.Sdk`) |
| Drizzle schema / Supabase migration | EF Core entity in `SaBai.API/Domain` + migration in `SaBai.API/Data/Migrations` |
| Supabase storage                  | RustFS (S3)                                                          |
| Face search (Supabase/pgvector)   | Qdrant                                                               |
| Supabase auth / RLS policies      | WorkOS (or the fake directory) + `Roles(...)` on each endpoint       |
| Root `src/`, `package.json`       | `SaBai.Web/`                                                         |

## Notes

- Upstream #303 (Supabase Realtime `SELECT` policy for the `notifications` topic) has no
  equivalent here: there is no Realtime channel, and broadcast endpoints already require an
  authenticated staff role.
- Upstream #278 changed `.husky/pre-commit` to run `lint-staged`; only the `lint-staged`
  config was ported because this repo has no husky hook.

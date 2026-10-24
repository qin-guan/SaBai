---
title: Developer Guide
excerpt: Quick orientation to the project
permalink: /docs/developer-guide/
---

This guide helps orientate new developers to the project.

## 1. What we use in this project

- Next.js (Pages Router): Web framework and routing.
- React: UI rendering and component model.
- @sabai/sdk: Generated, typesafe client for SaBai.API.
- @tanstack/react-query: Client data fetching, caching, and mutations.
- WorkOS (via SaBai.API): Authentication; the API issues a JWT bearer token.
- Zod: Runtime input validation for forms.
- Tailwind CSS: Utility-first styling.
- react-hook-form: Form state management.

> The web app is a thin client. Data access, authentication and the database all
> live in the separate **SaBai.API** (.NET) service. See
> [API SDK & Authentication]({{ '/docs/api-and-auth/' | relative_url }}).

## 2. Quick start (local)

- Follow the step-by-step instructions in README.
- Key commands:
  - Install: `pnpm i`
  - Run the API + dependencies: `aspire start` (or `dotnet run --project SaBai.API`)
  - Run the web dev server: `pnpm dev`
  - See script details in [Appendix A: package.json scripts](#appendix-a-packagejson-scripts).

See [README.md](https://github.com/NUS-Project-SaBai/project-sabai/blob/dev/README.md) for full details and local credentials.

## 3. Project layout

```
SaBai.Web/
|-- src/
|   |-- pages/                 # Next.js routes
|   |   |-- api/
|   |   |   |-- hello.ts       # example API route (unused)
|   |   |-- index.tsx          # Home page
|   |-- components/            # Shared UI components
|   |   |-- interactive/RHF/   # RHF input components
|   |-- lib/
|   |   |-- api/               # SDK config, token storage, query client, errors
|   |   |-- context/           # Cross-page React context (patients, village codes)
|   |   |-- utils/             # Formatting helpers
|   |-- types/                 # UI-facing types
|   |-- proxy.ts               # Route protection middleware (cookie gate)
```

Quick pointers:

- New page: add a file in `src/pages`.
- New UI component: add to `src/components`.
- New API call: use a `@sabai/sdk` hook (see the SDK guide).

## 4. Common tasks

### Add a new page that calls the backend

1. Find (or add) the endpoint in **SaBai.API**.
2. Regenerate the SDK if the endpoint is new (see the SDK guide).
3. Call it from the page with `useQuery` / `useMutation` and the shared
   `apiConfig`, e.g.:
   ```ts
   useQuery(saBaiAPIEndpointsPatientsListGetEndpointQueryOptions({}, apiConfig));
   ```

### Add a new UI component

- Place shared components in `src/components`.

### Run and open this documentation site

This site is built with Jekyll. To preview it locally, run these commands from
the `docs/` folder:

1. Install the [Jekyll prerequisites](https://jekyllrb.com/docs/installation/) (Ruby + Bundler).
2. `bundle install`
3. `bundle exec jekyll serve`
4. Open <http://localhost:4000> in your browser.

### Regenerate the API client

```bash
pnpm --filter @sabai/sdk generate   # requires SaBai.API running on :5250
pnpm --filter @sabai/sdk build
```

## 5. Auth model (short)

- Login calls `POST /auth/login` and stores the returned JWT in the
  `sabai_token` cookie.
- `src/proxy.ts` gates routes on the cookie and redirects to `/login`.
- The SDK attaches the token as `Authorization: Bearer …`; a `401` clears it and
  redirects to `/login`.

See [API SDK & Authentication]({{ '/docs/api-and-auth/' | relative_url }}) for the full flow.

## 6. Tips

- Always pass `apiConfig` to SDK hooks.
- Use `src/lib/api/errors.ts` helpers to surface API error messages.
- List endpoints often wrap their payload (e.g. `{ patients: [...] }`).
- Keep pages focused by feature domain.

## Appendix A: package.json scripts

Use these commands from the project root:

| Script            | What it does                                             | When to use                                                       |
| ----------------- | -------------------------------------------------------- | ----------------------------------------------------------------- |
| `pnpm dev`        | Starts Next.js in development mode.                      | Day-to-day local development.                                     |
| `pnpm build`      | Builds the production bundle.                            | Before deploy checks and CI validation.                           |
| `pnpm start`      | Runs the production build.                               | Local production-like smoke testing after `pnpm build`.           |
| `pnpm lint`       | Runs ESLint checks.                                      | Runs before push.                                                 |
| `pnpm format`     | Formats source files with Prettier.                      | Runs before every commit.                                         |
| `pnpm test:run`   | Runs the Vitest suite once.                              | CI / pre-push verification.                                       |

# Project Sa'bai — Web

Next.js (Pages Router) client for **SaBai.API**. It reads and writes all data
through the generated [`@sabai/sdk`](../SaBai.Sdk) client and authenticates with
the API's session cookie (ASP.NET Core Identity). It no longer talks to a
database or Supabase directly.

---

## Prerequisites

1. **Node.js 20+**
2. **pnpm**
   ```bash
   npm install -g pnpm
   ```
3. **.NET 10 SDK** and **Docker** — to run `SaBai.API` (plus Postgres, Qdrant and
   rustfs via the Aspire AppHost).

---

## Quick Start (Local Development)

### 1. Install dependencies

```bash
pnpm i
```

### 2. Start the backend

From the repository root, start the API and its dependencies with Aspire:

```bash
aspire start
```

Or run just the API:

```bash
dotnet run --project SaBai.API
```

The API listens on `http://localhost:5250` and serves OpenAPI at
`http://localhost:5250/openapi/v1.json`.

> **Local auth:** run the API with `Auth__SeedDemoUsers=true` to seed the demo
> staff accounts below. `dotnet run --project SaBai.AppHost` does this for you.

### 3. Configure environment variables

```bash
cp .env.example .env
```

| Variable  | Description                                                        |
| --------- | ------------------------------------------------------------------ |
| `API_URL` | Server-side URL of SaBai.API (default `http://localhost:5250`).    |

The browser calls this app under `/api`; `next.config.ts` rewrites those
requests to `API_URL`, so the API's session cookie is first-party. The rewrite
is read when `next dev` starts, and baked in by `next build`.

### 4. Run the dev server

```bash
pnpm dev
```

Open [http://localhost:3000](http://localhost:3000).

## Local Development Accounts

When the API runs with `Auth:SeedDemoUsers=true` it seeds these accounts
(password `Password1!`). Sign in with the username or the email.

| Role   | Username       | Email                     |
| ------ | -------------- | ------------------------- |
| admin  | `admin`        | `admin@sabai.test`        |
| member | `doctor`       | `doctor@sabai.test`       |
| member | `pharmacist`   | `pharmacist@sabai.test`   |
| member | `registration` | `registration@sabai.test` |

---

## Authentication

- `POST /api/auth/login` (username or email + password) sets the HttpOnly
  `sabai.auth` cookie and returns `{ requiresTwoFactor, user }`. Accounts with
  two-factor on finish with `POST /api/auth/login/two-factor` (or
  `/auth/login/recovery-code`).
- The browser sends the cookie on every SDK request; no token is handled in JS.
- `src/proxy.ts` protects routes; a `401` signs out (`POST /auth/logout`) and
  redirects to `/login`.
- Staff accounts, roles and two-factor are managed in the Nuxt app
  (`SaBai.Web.Nuxt`: Settings → Staff Users, and My Account).

See [API SDK & Authentication](docs/_docs/03-api-and-auth.md) for details.

## Testing the backend API with Postman

1. `POST http://localhost:5250/auth/login` with JSON body:
   ```json
   { "userName": "admin", "password": "Password1!" }
   ```
2. Postman keeps the `sabai.auth` cookie from the response and sends it on
   later requests to the same host.

## Regenerating the API client

After changing SaBai.API, regenerate `@sabai/sdk` from its live OpenAPI document:

```bash
pnpm --filter @sabai/sdk generate
pnpm --filter @sabai/sdk build
```

## Testing

This project uses Vitest with React Testing Library for unit and integration
testing.

```bash
pnpm test          # watch mode
pnpm test:run      # run once
pnpm test:ui       # UI
pnpm test:coverage # coverage
```

- **Setup:** `vitest.config.mjs` with the setup file `src/test-setup.ts`.
- **Test files:** `src/**/__tests__/**/*.{test,spec}.{ts,tsx}`.
- Rendering components that fetch data requires a React Query client — use the
  `renderWithQueryClient` helper in `src/__tests__/utils/renderWithQueryClient.tsx`.

```tsx
import { renderWithQueryClient, screen } from "@/__tests__/utils/renderWithQueryClient";
import DummyComponent from "@/components/DummyComponent";

describe("DummyComponent", () => {
  it("renders correctly", () => {
    renderWithQueryClient(<DummyComponent />);
    expect(screen.getByText("Expected Text")).toBeInTheDocument();
  });
});
```

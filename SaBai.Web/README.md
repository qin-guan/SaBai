# Project Sa'bai — Web

Next.js (Pages Router) client for **SaBai.API**. It reads and writes all data
through the generated [`@sabai/sdk`](../SaBai.Sdk) client and authenticates with
the JWT issued by the API (WorkOS-backed). It no longer talks to a database or
Supabase directly.

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

> **Local auth:** run the API with `WorkOS__UseFake=true` to use the seeded staff
> accounts instead of real WorkOS credentials.

### 3. Configure environment variables

```bash
cp .env.example .env
```

| Variable              | Description                        |
| --------------------- | ---------------------------------- |
| `NEXT_PUBLIC_API_URL` | Base URL of SaBai.API (e.g. `http://localhost:5250`). |

The API allows the web origin through its CORS policy (`Cors:AllowedOrigins`,
defaults to `http://localhost:3000`).

### 4. Run the dev server

```bash
pnpm dev
```

Open [http://localhost:3000](http://localhost:3000).

## Local Development Accounts

When the API runs with `WorkOS:UseFake=true` it seeds these accounts
(password `Password1!`):

| Role     | Email                       |
| -------- | --------------------------- |
| Admin    | `admin@sabai.test`          |
| Doctor   | `doctor@sabai.test`         |
| Pharmacist | `pharmacist@sabai.test`   |
| Registration | `registration@sabai.test` |

---

## Authentication

- `POST /auth/login` (email + password) returns `{ accessToken, expiresAt, user }`.
- The web app stores the token in the `sabai_token` cookie and sends it as
  `Authorization: Bearer …` on every SDK request.
- `src/proxy.ts` protects routes; a `401` clears the token and redirects to
  `/login`.

See [API SDK & Authentication](docs/_docs/03-api-and-auth.md) for details.

## Testing the backend API with Postman

1. `POST http://localhost:5250/auth/login` with JSON body:
   ```json
   { "email": "admin@sabai.test", "password": "Password1!" }
   ```
2. Copy `accessToken` from the response.
3. Add header `Authorization: Bearer <accessToken>` to subsequent requests.

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

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import type { RenderOptions } from "@testing-library/react";
import type { ReactNode } from "react";

/**
 * Creates a react-query client configured for tests.
 *
 * Failures should surface immediately, so query retries are disabled.
 * Tests that need to inspect or pre-seed the cache can pass their own client
 * to {@link renderWithQueryClient}.
 */
export function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });
}

export type RenderWithQueryClientOptions = Omit<RenderOptions, "wrapper"> & {
  /** Optional client to reuse (e.g. to pre-seed the cache or inspect it). */
  queryClient?: QueryClient;
};

/**
 * Renders `ui` inside a `QueryClientProvider` so that components which use
 * `@tanstack/react-query` + `@sabai/sdk` have a client available.
 *
 * Use this instead of `render` everywhere in the suite.
 */
export function renderWithQueryClient(
  ui: ReactNode,
  options?: RenderWithQueryClientOptions,
) {
  const { queryClient = createTestQueryClient(), ...renderOptions } =
    options ?? {};

  return render(
    <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>,
    renderOptions,
  );
}

// Re-export the testing-library surface so tests can `screen`, `fireEvent`,
// `waitFor`, etc. from a single import.
export * from "@testing-library/react";

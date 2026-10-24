import { MutationCache, QueryCache, QueryClient } from "@tanstack/react-query";
import { isUnauthorizedError } from "./errors";
import { clearToken } from "./token";

/**
 * Clears the stored token and sends the user to the login page, preserving the
 * current location so they can be returned after signing back in.
 */
function redirectToLogin(): void {
  if (typeof window === "undefined") return;
  if (window.location.pathname === "/login") return;

  clearToken();

  const redirectTo = window.location.pathname + window.location.search;
  window.location.href = `/login?redirectTo=${encodeURIComponent(redirectTo)}`;
}

/**
 * Application-wide React Query client.
 *
 * @tanstack/react-query is required because @sabai/sdk exposes its hooks as
 * `queryOptions` / `mutationOptions` factories that are consumed with `useQuery`
 * and `useMutation` directly.
 *
 * A 401 from the API means the bearer token is missing or expired, so we drop it
 * and bounce to the login page from a single place rather than in every hook.
 */
export function makeQueryClient(): QueryClient {
  return new QueryClient({
    queryCache: new QueryCache({
      onError: (error) => {
        if (isUnauthorizedError(error)) redirectToLogin();
      },
    }),
    mutationCache: new MutationCache({
      onError: (error) => {
        if (isUnauthorizedError(error)) redirectToLogin();
      },
    }),
    defaultOptions: {
      queries: {
        refetchOnWindowFocus: false,
        retry: (failureCount, error) =>
          !isUnauthorizedError(error) && failureCount < 2,
      },
    },
  });
}

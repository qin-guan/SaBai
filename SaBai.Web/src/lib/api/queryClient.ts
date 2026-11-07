import { MutationCache, QueryCache, QueryClient } from "@tanstack/react-query";
import { isUnauthorizedError } from "./errors";
import { endSession } from "./session";

let redirecting = false;

/**
 * Clears the (expired or revoked) session cookie and sends the user to the
 * login page, preserving the current location so they can be returned after
 * signing back in. The cookie must be cleared first, or the route middleware
 * would see it and bounce straight back.
 */
function redirectToLogin(): void {
  if (typeof window === "undefined") return;
  if (window.location.pathname === "/login" || redirecting) return;
  redirecting = true;

  const redirectTo = window.location.pathname + window.location.search;
  void endSession().finally(() => {
    window.location.href = `/login?redirectTo=${encodeURIComponent(redirectTo)}`;
  });
}

/**
 * Application-wide React Query client.
 *
 * @tanstack/react-query is required because @sabai/sdk exposes its hooks as
 * `queryOptions` / `mutationOptions` factories that are consumed with `useQuery`
 * and `useMutation` directly.
 *
 * A 401 from the API means the session is missing, expired or was revoked, so
 * we bounce to the login page from a single place rather than in every hook.
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

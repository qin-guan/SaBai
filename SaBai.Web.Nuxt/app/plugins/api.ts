import { MutationCache, QueryCache, QueryClient, VueQueryPlugin } from "@tanstack/vue-query";
import { getApiErrorStatus } from "~/utils/apiErrors";
import { isNetworkError } from "~/offline/network";
import { createQueryPersistence, QUERY_CACHE_MAX_AGE_MS } from "~/offline/queryPersistence";
import { initOfflineRuntime } from "~/offline/runtime";
// Registers every offline mutation definition before anything can replay one.
import "~/offline/mutations";

/**
 * Sends the user to the login page, preserving the current location so they
 * can be returned after signing back in. A full page load also drops every
 * cached query from the expired session.
 */
function redirectToLogin(): void {
  if (window.location.pathname === "/login") return;
  const redirectTo = window.location.pathname + window.location.search;
  window.location.href = `/login?redirectTo=${encodeURIComponent(redirectTo)}`;
}

/**
 * Retry a failed query twice, except when retrying cannot help: signed out,
 * the server refused the request (4xx), or the server is unreachable (the
 * offline runtime refetches when the connection comes back, so retrying now
 * would only pile up requests on a dead network).
 */
function shouldRetry(failureCount: number, error: unknown): boolean {
  if (isNetworkError(error)) return false;
  const status = getApiErrorStatus(error);
  if (status !== undefined && status >= 400 && status < 500) return false;
  return failureCount < 2;
}

export default defineNuxtPlugin({
  name: "sabai:api",
  async setup(nuxtApp) {
    // A 401 means the session cookie is missing, expired or was revoked (locked
    // account, password reset): bounce to the login page from one place rather
    // than in every query. The session check itself opts out via `meta`, because
    // the route middleware handles a signed-out user there. A network error is
    // never a 401, so being offline never signs anyone out.
    const queryClient = new QueryClient({
      queryCache: new QueryCache({
        onError: (error, query) => {
          if (isUnauthorizedError(error) && !query.meta?.skipAuthRedirect) redirectToLogin();
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
          // Cached data is kept (and persisted) for a week so it can be read offline.
          gcTime: QUERY_CACHE_MAX_AGE_MS,
          // Always try the request once, so a lying `navigator.onLine` cannot stop it;
          // retries pause while offline. A failed refetch keeps the cached data.
          networkMode: "offlineFirst",
          retry: shouldRetry,
        },
        mutations: {
          // Online-only writes fail fast with a network error instead of waiting
          // paused while offline. Offline-capable writes use useOfflineMutation.
          networkMode: "always",
          retry: false,
        },
      },
    });

    // Restore the cache saved on this device before any page reads it.
    const persistence = createQueryPersistence(queryClient, {
      buster: useRuntimeConfig().app.buildId,
    });
    await persistence.restore();
    persistence.start();

    await initOfflineRuntime({
      queryClient,
      clearPersistedQueries: persistence.clear,
      // A replayed write got 401: the session ended. Whoever signs in next gets
      // their own writes sent; anyone else's stay parked on the device.
      onUnauthorized: redirectToLogin,
    });

    nuxtApp.vueApp.use(VueQueryPlugin, { queryClient });

    return { provide: { queryClient } };
  },
});

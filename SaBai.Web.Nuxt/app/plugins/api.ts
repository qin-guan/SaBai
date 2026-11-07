import { MutationCache, QueryCache, QueryClient, VueQueryPlugin } from "@tanstack/vue-query";

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

export default defineNuxtPlugin((nuxtApp) => {
  // A 401 means the session cookie is missing, expired or was revoked (locked
  // account, password reset): bounce to the login page from one place rather
  // than in every query. The session check itself opts out via `meta`, because
  // the route middleware handles a signed-out user there.
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
        retry: (failureCount, error) => !isUnauthorizedError(error) && failureCount < 2,
      },
    },
  });

  nuxtApp.vueApp.use(VueQueryPlugin, { queryClient });

  return { provide: { queryClient } };
});

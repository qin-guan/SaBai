import { MutationCache, QueryCache, QueryClient, VueQueryPlugin } from "@tanstack/vue-query";

/**
 * Clears the stored token and sends the user to the login page, preserving the
 * current location so they can be returned after signing back in.
 */
function redirectToLogin(): void {
  if (window.location.pathname === "/login") return;
  clearToken();
  const redirectTo = window.location.pathname + window.location.search;
  window.location.href = `/login?redirectTo=${encodeURIComponent(redirectTo)}`;
}

export default defineNuxtPlugin((nuxtApp) => {
  // A 401 means the bearer token is missing or expired: drop it and bounce to
  // the login page from one place rather than in every query.
  const queryClient = new QueryClient({
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
        retry: (failureCount, error) => !isUnauthorizedError(error) && failureCount < 2,
      },
    },
  });

  nuxtApp.vueApp.use(VueQueryPlugin, { queryClient });
});

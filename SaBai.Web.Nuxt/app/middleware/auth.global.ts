import { toast } from "vue-sonner";

/**
 * Route protection. The session is an HttpOnly cookie JavaScript cannot read,
 * so the guard asks the API who is signed in (cached by the query client).
 * Pages with `definePageMeta({ requiresAdmin: true })` are admin-only.
 */
export default defineNuxtRouteMiddleware(async (to) => {
  const { $queryClient } = useNuxtApp();
  const user = await loadCurrentUser($queryClient);
  const isLoginRoute = to.path === paths.login();

  if (!user && !isLoginRoute) {
    return navigateTo({ path: paths.login(), query: { redirectTo: to.fullPath } });
  }

  if (user && isLoginRoute) {
    return navigateTo(paths.home());
  }

  if (to.meta.requiresAdmin && !isAdmin(user)) {
    // On a full page load the toaster has not mounted yet; wait for the app.
    onNuxtReady(() => toast.error("Only admins can open that page."));
    return navigateTo(paths.home());
  }
});

declare module "#app" {
  interface PageMeta {
    /** Only users with the admin role may open this page. */
    requiresAdmin?: boolean;
  }
}

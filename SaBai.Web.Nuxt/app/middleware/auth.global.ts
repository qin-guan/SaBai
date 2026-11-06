/**
 * Route protection. Only checks that the token cookie is present: validity is
 * enforced by the API, and a 401 is turned into a redirect by the query client.
 */
export default defineNuxtRouteMiddleware((to) => {
  const token = getToken();
  const isLoginRoute = to.path === "/login";

  if (!token && !isLoginRoute) {
    return navigateTo({ path: "/login", query: { redirectTo: to.fullPath } });
  }

  if (token && isLoginRoute) {
    return navigateTo("/");
  }
});

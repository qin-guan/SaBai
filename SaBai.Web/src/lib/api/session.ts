/**
 * Session handling for the SaBai API.
 *
 * `POST /auth/login` sets an HttpOnly cookie (ASP.NET Core Identity). The
 * browser reaches the API through this app's `/api` rewrite (next.config.ts),
 * so the cookie belongs to this origin: the route-protection middleware can see
 * it, every SDK request sends it automatically, and no script can read it.
 */
export const SESSION_COOKIE_NAME = "sabai.auth";

/**
 * Asks the API to clear the session cookie. JavaScript cannot delete an
 * HttpOnly cookie itself, so this is the only way to sign out.
 */
export async function endSession(): Promise<void> {
  try {
    await fetch("/api/auth/logout", { method: "POST", credentials: "same-origin" });
  } catch {
    // Offline or API down: the cookie expires on its own.
  }
}

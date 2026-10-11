/**
 * Bearer token storage for the SaBai API.
 *
 * The API issues a JWT from `POST /auth/login`. We keep it in a cookie (rather
 * than localStorage) so the route-protection middleware can read it, while still
 * remaining readable from the browser so @sabai/sdk can attach it as a
 * `Authorization: Bearer` header. It is intentionally not HttpOnly: the SDK runs
 * in the browser and must read it to sign requests.
 */

export const TOKEN_COOKIE_NAME = "sabai_token";

/** Default cookie lifetime if the API does not report an expiry. */
const DEFAULT_MAX_AGE_SECONDS = 60 * 60 * 8;

export function getToken(): string | undefined {
  if (typeof document === "undefined") return undefined;

  const match = document.cookie.match(
    new RegExp(`(?:^|; )${TOKEN_COOKIE_NAME}=([^;]*)`),
  );

  return match ? decodeURIComponent(match[1]) : undefined;
}

export function setToken(token: string, expiresAt?: string): void {
  if (typeof document === "undefined") return;

  let maxAge = DEFAULT_MAX_AGE_SECONDS;
  if (expiresAt) {
    const seconds = Math.floor((new Date(expiresAt).getTime() - Date.now()) / 1000);
    if (Number.isFinite(seconds) && seconds > 0) maxAge = seconds;
  }

  document.cookie = `${TOKEN_COOKIE_NAME}=${encodeURIComponent(token)}; path=/; max-age=${maxAge}; SameSite=Lax`;
}

export function clearToken(): void {
  if (typeof document === "undefined") return;

  document.cookie = `${TOKEN_COOKIE_NAME}=; path=/; max-age=0; SameSite=Lax`;
}

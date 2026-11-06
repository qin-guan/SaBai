/**
 * Bearer token storage for the SaBai API.
 *
 * The API issues a JWT from `POST /auth/login`. It is kept in a (non-HttpOnly)
 * cookie so the route middleware can check for it and the SDK can attach it as
 * an `Authorization: Bearer` header — same contract as SaBai.Web.
 */
export const TOKEN_COOKIE_NAME = "sabai_token";

/** Default cookie lifetime if the API does not report an expiry. */
const DEFAULT_MAX_AGE_SECONDS = 60 * 60 * 8;

export function getToken(): string | undefined {
  if (typeof document === "undefined") return undefined;
  const match = document.cookie.match(new RegExp(`(?:^|; )${TOKEN_COOKIE_NAME}=([^;]*)`));
  return match ? decodeURIComponent(match[1]!) : undefined;
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

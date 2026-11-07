/**
 * Shared configuration passed to every @sabai/sdk call (queries and mutations).
 *
 * The generated SDK does not export a configurable client instance, so the
 * `baseURL` is supplied per call through the `config` argument of the generated
 * `*QueryOptions` / `*MutationOptions` helpers. `/api` is rewritten to SaBai.API
 * by next.config.ts, so the session cookie is same-origin and sent by the
 * browser without any token handling here.
 */
export const apiConfig = {
  baseURL: "/api",
  credentials: "same-origin" as const,
};

/**
 * Reverse proxy to SaBai.API.
 *
 * The API sits on an internal network the browser cannot reach, so the app
 * calls this server on the same origin under `/api` and every request is
 * forwarded as-is (method, headers incl. the session cookie, streamed body) to
 * `runtimeConfig.apiUrl` with the `/api` prefix stripped:
 *
 *   GET /api/patients/1?x=y  →  GET {NUXT_API_URL}/patients/1?x=y
 */
const PREFIX = "/api";

export default defineEventHandler((event) => {
  const apiUrl = (useRuntimeConfig(event).apiUrl as string).replace(/\/+$/, "");
  // event.path keeps the original encoding and query string.
  const path = event.path.startsWith(PREFIX) ? event.path.slice(PREFIX.length) : event.path;

  return proxyRequest(event, apiUrl + (path.startsWith("/") ? path : `/${path}`), {
    // Stream request bodies (photo uploads) instead of buffering them in memory.
    streamRequest: true,
  });
});

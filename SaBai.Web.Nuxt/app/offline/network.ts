import { getApiErrorMessage, getApiErrorStatus } from '~/utils/apiErrors'

/**
 * Deciding whether a request failed because the server could not be reached.
 *
 * `navigator.onLine` is not trusted: on clinic Wi-Fi without an uplink it says
 * "online" while every request fails. What counts is the outcome of a real
 * request:
 * - `fetch` rejected (no route, DNS, connection reset, CORS-less proxy error).
 *   The API transport (`apiTransport`) rethrows these as {@link NetworkError}.
 * - the request timed out (`TimeoutError`, set by the transport).
 * - the Nitro `/api` proxy answered 502 (SaBai.API unreachable), or a gateway
 *   in front of it answered 503/504.
 *
 * A request the caller aborted (`AbortError`, e.g. a query that was cancelled)
 * is not a network failure.
 */

/** HTTP statuses that mean "the API did not answer", not "the API said no". */
export const GATEWAY_STATUSES: ReadonlySet<number> = new Set([502, 503, 504])

/** Thrown by the API transport when `fetch` itself fails. `cause` is the original error. */
export class NetworkError extends Error {
  constructor(message = 'The server could not be reached.', options?: { cause?: unknown }) {
    super(message, options)
    this.name = 'NetworkError'
  }
}

// Messages browsers use for a fetch that never got a response.
const FETCH_FAILURE = /failed to fetch|fetch failed|networkerror|network error|load failed|network request failed|the internet connection appears to be offline/i

function errorName(error: unknown): string | undefined {
  return error && typeof error === 'object' && 'name' in error && typeof error.name === 'string'
    ? error.name
    : undefined
}

/**
 * True when `error` means the server could not be reached (as opposed to the
 * server answering with an error). Shared by offline mutations and queries.
 */
export function isNetworkError(error: unknown): boolean {
  if (!error || typeof error !== 'object') return false
  const name = errorName(error)
  if (name === 'NetworkError' || name === 'TimeoutError') return true
  const status = getApiErrorStatus(error)
  if (status !== undefined) return GATEWAY_STATUSES.has(status)
  if (error instanceof TypeError) return FETCH_FAILURE.test(error.message)
  return false
}

/** True when the caller cancelled the request (not a failure of the request). */
export function isAbortError(error: unknown): boolean {
  return errorName(error) === 'AbortError'
}

/**
 * How a replayed (queued) write failed:
 * - `stop`: try again later and keep the order (network failure, timeout,
 *   401 signed out, 408, 429).
 * - `conflict`: 412, the record was changed by someone else after this device
 *   last heard from the server.
 * - `rejected`: the server refused it (any other 4xx, a 5xx that is not a
 *   gateway error, or an error thrown by the mutation's own code). Needs a person.
 */
export type ReplayFailure = 'stop' | 'conflict' | 'rejected'

export function classifyReplayError(error: unknown): ReplayFailure {
  if (isNetworkError(error) || isAbortError(error)) return 'stop'
  const status = getApiErrorStatus(error)
  if (status === 412) return 'conflict'
  if (status === 401 || status === 408 || status === 429) return 'stop'
  return 'rejected'
}

/** A short message for a failed write, shown on the sync page. */
export function describeWriteError(error: unknown): string {
  const status = getApiErrorStatus(error)
  if (status === 412) {
    return getApiErrorMessage(error, 'Someone else changed this record after this device last synced.')
  }
  return getApiErrorMessage(error, error instanceof Error ? error.message : 'The server refused this change.')
}

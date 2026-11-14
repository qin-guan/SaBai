import { readLocal, writeLocal } from './storage'

/**
 * The server's clock as seen from this device.
 *
 * Every successful response through the API transport records its `Date`
 * header (the Nitro `/api` proxy passes SaBai.API's header through) with the
 * device time it arrived. That pair converts a device time — when cached data
 * was fetched — into server time ({@link serverTimeAt}). A write sends
 * `If-Unmodified-Since: <server time its data was fetched>` and the server
 * answers 412 when a record the write changes was changed by someone else
 * since. Using the server's clock means a wrong device clock does not matter.
 * Persisted so a reload while offline keeps it.
 */

const STORAGE_KEY = 'sabai.offline.serverTime'

interface ServerTimeState {
  /** Last `Date` header value (an HTTP-date, as sent by the server). */
  serverDate?: string
  /** Device time (ms) when it was received. */
  receivedAt?: number
}

let state: ServerTimeState | undefined

function load(): ServerTimeState {
  state ??= readLocal<ServerTimeState>(STORAGE_KEY) ?? {}
  return state
}

/** Records a `Date` response header. Ignores missing or unparsable values and values older than the last one. */
export function recordServerDate(dateHeader: string | null | undefined, receivedAt = Date.now()): void {
  if (!dateHeader) return
  const parsed = Date.parse(dateHeader)
  if (Number.isNaN(parsed)) return
  const current = load()
  if (current.serverDate && Date.parse(current.serverDate) > parsed) return
  state = { serverDate: new Date(parsed).toUTCString(), receivedAt }
  writeLocal(STORAGE_KEY, state)
}

/**
 * The server's time at device time `deviceMs`, as an HTTP-date (whole seconds,
 * rounded down), or undefined if this device never reached the server. The
 * `Date` header is itself rounded down and was sent before it arrived, so the
 * result is never later than the true server time: a check against it can
 * only err towards a conflict.
 */
export function serverTimeAt(deviceMs: number): string | undefined {
  const { serverDate, receivedAt } = load()
  if (!serverDate || receivedAt === undefined) return undefined
  const offset = Date.parse(serverDate) - receivedAt
  return new Date(Math.floor((deviceMs + offset) / 1000) * 1000).toUTCString()
}

/** Device time (ms) of the last successful API response, if any. */
export function getLastServerContactAt(): number | undefined {
  return load().receivedAt
}

export function clearServerTime(): void {
  state = {}
  writeLocal(STORAGE_KEY, undefined)
}

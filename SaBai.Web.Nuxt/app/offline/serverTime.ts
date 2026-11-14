import { readLocal, writeLocal } from './storage'

/**
 * The server's clock as of this device's last successful API response.
 *
 * Every successful response through the API transport records its `Date`
 * header (the Nitro `/api` proxy passes SaBai.API's header through). A write
 * that gets queued is stamped with this value as its `baseTime`, and replaying
 * it sends `If-Unmodified-Since: <baseTime>`: the server answers 412 when a
 * record the write touches was changed by someone else after this device last
 * heard from it. The server's clock is used so a wrong device clock does not
 * matter. Persisted so a reload while offline keeps it.
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

/** The value for `If-Unmodified-Since` (an HTTP-date), or undefined if this device never reached the server. */
export function getBaseTime(): string | undefined {
  return load().serverDate
}

/** Device time (ms) of the last successful API response, if any. */
export function getLastServerContactAt(): number | undefined {
  return load().receivedAt
}

export function clearServerTime(): void {
  state = {}
  writeLocal(STORAGE_KEY, undefined)
}

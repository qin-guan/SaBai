import type { QueryClient } from '@tanstack/vue-query'
import { readLocal, writeLocal } from './storage'

/**
 * Downloading the working set for offline use.
 *
 * Each domain registers, at module top level next to its offline mutations,
 * what it needs on a device without a connection:
 *
 * ```ts
 * registerOfflinePrefetch('village-codes', qc => qc.fetchQuery(villageCodesQueryOptions(false)).then(() => {}))
 * ```
 *
 * Use `fetchQuery` (not `prefetchQuery`, which swallows errors) with the same
 * query options the screens use, so the data lands under the same keys. Give
 * `roles` (as in the endpoint's `Roles(...)`) when not every staff role may
 * read what it downloads; it is skipped for users without one of them. The
 * runner runs them with bounded concurrency after sign-in while online (at
 * most once per {@link PREFETCH_MIN_INTERVAL_MS}) and from "Download for
 * offline use" on the sync page.
 */

export type OfflinePrefetchFn = (queryClient: QueryClient) => Promise<void>

export interface OfflinePrefetchOptions {
  /** Staff roles that may read what it downloads; omitted: every signed-in user. */
  roles?: readonly string[]
}

export interface RunPrefetchOptions {
  concurrency?: number
  /** The signed-in user's roles: prefetches limited to other roles are skipped. */
  roles?: readonly string[]
}

export interface PrefetchState {
  running: boolean
  total: number
  done: number
  /** Names of the prefetches that failed in the current or last run. */
  failed: string[]
  /** Device time (ms) the last run finished. */
  lastRunAt?: number
}

/** The roles the API lets read clinical records and clinic lists (villages, medication, visits). */
export const CLINIC_STAFF_ROLES: readonly string[] = ['admin', 'member']

export const PREFETCH_MIN_INTERVAL_MS = 30 * 60 * 1000
export const PREFETCH_CONCURRENCY = 3
const LAST_RUN_KEY = 'sabai.offline.prefetchedAt'

const prefetches = new Map<string, { fn: OfflinePrefetchFn, roles?: readonly string[] }>()
const listeners = new Set<(state: PrefetchState) => void>()
let state: PrefetchState = { running: false, total: 0, done: 0, failed: [], lastRunAt: readLocal<number>(LAST_RUN_KEY) }
let current: Promise<PrefetchState> | undefined
/** Device time (ms) of the last automatic attempt, complete or not (not persisted). */
let lastAttemptAt: number | undefined

function setState(next: PrefetchState) {
  state = next
  for (const listener of listeners) listener(state)
}

/** Registers (or, under the same name, replaces) a prefetch. Call at module top level. */
export function registerOfflinePrefetch(name: string, fn: OfflinePrefetchFn, options: OfflinePrefetchOptions = {}): void {
  prefetches.set(name, { fn, roles: options.roles })
}

export function getPrefetchState(): PrefetchState {
  return state
}

export function subscribePrefetch(listener: (state: PrefetchState) => void): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

/**
 * Runs every registered prefetch the user's `roles` allow, at most
 * `concurrency` at a time. Concurrent calls share one run.
 */
export function runOfflinePrefetch(
  queryClient: QueryClient,
  { concurrency = PREFETCH_CONCURRENCY, roles }: RunPrefetchOptions = {},
): Promise<PrefetchState> {
  current ??= (async () => {
    const queue = [...prefetches.entries()]
      .filter(([, entry]) => !entry.roles || entry.roles.some(role => roles?.includes(role)))
      .map(([name, entry]) => [name, entry.fn] as const)
    const failed: string[] = []
    let done = 0
    setState({ ...state, running: true, total: queue.length, done: 0, failed: [] })

    async function worker() {
      for (let next = queue.shift(); next; next = queue.shift()) {
        const [name, fn] = next
        try {
          await fn(queryClient)
        }
        catch (error) {
          console.warn(`Offline download "${name}" failed`, error)
          failed.push(name)
        }
        done++
        setState({ ...state, done, failed: [...failed] })
      }
    }

    await Promise.all(Array.from({ length: Math.max(1, Math.min(concurrency, queue.length)) }, worker))
    const lastRunAt = Date.now()
    if (failed.length === 0) writeLocal(LAST_RUN_KEY, lastRunAt)
    setState({ ...state, running: false, lastRunAt: failed.length === 0 ? lastRunAt : state.lastRunAt })
    return state
  })().finally(() => {
    current = undefined
  })
  return current
}

/**
 * Runs the prefetch unless a complete run finished, or an automatic attempt
 * started, less than `minIntervalMs` ago.
 */
export function maybeRunOfflinePrefetch(
  queryClient: QueryClient,
  { minIntervalMs = PREFETCH_MIN_INTERVAL_MS, now = Date.now(), roles }: { minIntervalMs?: number, now?: number, roles?: readonly string[] } = {},
): Promise<PrefetchState> | undefined {
  if (current) return current
  const recent = (time: number | undefined) => time !== undefined && now - time < minIntervalMs
  if (recent(state.lastRunAt) || recent(lastAttemptAt)) return undefined
  lastAttemptAt = now
  return runOfflinePrefetch(queryClient, { roles })
}

/** Forgets when the last download ran (sign-out, another user signed in), so the next user downloads their own. */
export function resetPrefetchState(): void {
  lastAttemptAt = undefined
  writeLocal(LAST_RUN_KEY, undefined)
  setState({ running: false, total: 0, done: 0, failed: [] })
}

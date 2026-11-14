import type { DehydrateOptions, Query, QueryClient } from '@tanstack/vue-query'
import {
  persistQueryClientRestore,
  persistQueryClientSubscribe,
  type PersistedClient,
  type Persister,
} from '@tanstack/query-persist-client-core'
import { createStore, del, get, set } from 'idb-keyval'

/**
 * Keeps the Vue Query cache in IndexedDB, so whatever this device has fetched
 * stays readable offline and after a reload.
 *
 * - Restored before the app renders (the API plugin awaits `restore()`).
 * - Kept for {@link QUERY_CACHE_MAX_AGE_MS}, and dropped when the app build
 *   changes (`buster`), since cached shapes may not match new code.
 * - Only queries with data are saved, never in an error state: a query whose
 *   refetch failed offline is saved as its last good data. Queries with
 *   `meta: { persist: false }` (face search, the session check…) are skipped.
 * - It holds patient data: `clear()` runs on sign-out (in every open tab). A
 *   save still waiting for its throttle is dropped then, so it cannot write
 *   the old data back.
 */

export const QUERY_CACHE_MAX_AGE_MS = 7 * 24 * 60 * 60 * 1000
const DB_NAME = 'sabai-query-cache'
const STORE_NAME = 'cache'
const KEY = 'vue-query'
const THROTTLE_MS = 1000

export function shouldPersistQuery(query: Pick<Query, 'state' | 'meta'>): boolean {
  return query.state.data !== undefined && query.meta?.persist !== false
}

/** Saves an errored query as its last good data, so it is restored as data, not as an error. */
export function sanitizePersistedClient(client: PersistedClient): PersistedClient {
  return {
    ...client,
    clientState: {
      ...client.clientState,
      mutations: [],
      queries: client.clientState.queries.map((query) => {
        const state = query.state
        return {
          ...query,
          state: {
            ...state,
            status: 'success',
            error: null,
            fetchStatus: 'idle',
            fetchFailureCount: 0,
            fetchFailureReason: null,
          },
        }
      }),
    },
  }
}

export interface QueryPersistence {
  restore: () => Promise<void>
  /** Starts saving cache changes. */
  start: () => void
  /** Stops saving, forgets the saved cache, and resumes saving the (now empty) cache. */
  clear: () => Promise<void>
}

export function createQueryPersistence(queryClient: QueryClient, { buster }: { buster: string }): QueryPersistence {
  const store = typeof indexedDB === 'undefined' ? undefined : createStore(DB_NAME, STORE_NAME)
  // Saves the latest state at most once per THROTTLE_MS (at once when idle,
  // otherwise at the end of the interval), and when the page is left. Removing
  // the saved cache also drops a save that is still waiting.
  let pending: PersistedClient | undefined
  let timer: ReturnType<typeof setTimeout> | undefined
  let lastSavedAt = 0
  function flush() {
    clearTimeout(timer)
    timer = undefined
    const client = pending
    pending = undefined
    if (!client || !store) return
    lastSavedAt = Date.now()
    set(KEY, JSON.stringify(sanitizePersistedClient(client)), store).catch((error) => {
      console.warn('Could not save the offline cache', error)
    })
  }
  if (typeof window !== 'undefined') window.addEventListener('pagehide', flush)
  const persister: Persister = {
    persistClient(client) {
      pending = client
      if (timer) return
      const wait = lastSavedAt + THROTTLE_MS - Date.now()
      if (wait <= 0) flush()
      else timer = setTimeout(flush, wait)
    },
    async restoreClient() {
      const raw = store ? await get<string>(KEY, store) : undefined
      return raw ? (JSON.parse(raw) as PersistedClient) : undefined
    },
    async removeClient() {
      clearTimeout(timer)
      timer = undefined
      pending = undefined
      if (store) await del(KEY, store)
    },
  }

  const dehydrateOptions: DehydrateOptions = {
    shouldDehydrateQuery: shouldPersistQuery,
    shouldDehydrateMutation: () => false,
  }
  const props = { queryClient, persister, buster, maxAge: QUERY_CACHE_MAX_AGE_MS, dehydrateOptions }
  let unsubscribe: (() => void) | undefined

  return {
    async restore() {
      try {
        await persistQueryClientRestore(props)
      }
      catch (error) {
        // A corrupt or unreadable cache is discarded; the app fetches afresh.
        console.warn('Could not restore the offline cache', error)
      }
    },
    start() {
      unsubscribe ??= persistQueryClientSubscribe(props)
    },
    async clear() {
      unsubscribe?.()
      unsubscribe = undefined
      queryClient.clear()
      await persister.removeClient()
      unsubscribe = persistQueryClientSubscribe(props)
    },
  }
}

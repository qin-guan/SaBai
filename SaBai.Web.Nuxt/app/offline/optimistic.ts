import type { Query, QueryClient, QueryKey, QueryState } from '@tanstack/vue-query'
import type { AnyOfflineMutationDef } from './defineOfflineMutation'
import type { OutboxItem } from './outbox'

/**
 * Runs `edit` and keeps each changed query's `dataUpdatedAt` as it was: an
 * optimistic edit does not make cached data newer, and that time is what a
 * later write's conflict check is based on. Returns the queries the edit
 * changed and their states from before it. An edit that throws is undone.
 */
function editKeepingDataAge(queryClient: QueryClient, edit: () => void) {
  const cache = queryClient.getQueryCache()
  const before = new Map(cache.getAll().map(query => [query.queryHash, query.state]))
  const changed = () => cache.getAll().filter(query => before.get(query.queryHash) !== query.state)
  try {
    edit()
  }
  catch (error) {
    for (const query of changed()) {
      const previous = before.get(query.queryHash)
      if (previous === undefined) cache.remove(query)
      else query.setState(previous)
    }
    throw error
  }
  for (const query of changed()) {
    const previous = before.get(query.queryHash)
    if (previous?.data !== undefined && query.state.dataUpdatedAt !== previous.dataUpdatedAt)
      query.setState({ ...query.state, dataUpdatedAt: previous.dataUpdatedAt })
  }
  return { before, touched: changed() }
}

/** The oldest time (device ms) one of these states' data was fetched; states without data are ignored. */
function oldestDataAge(states: readonly (QueryState | undefined)[]): number | undefined {
  const ages = states
    .filter((state): state is QueryState => state?.data !== undefined && state.dataUpdatedAt > 0)
    .map(state => state.dataUpdatedAt)
  return ages.length ? Math.min(...ages) : undefined
}

/** When the oldest of these cached queries was fetched (device ms), if any of them has data. */
export function cachedDataAge(queryClient: QueryClient, queryKeys: readonly QueryKey[]): number | undefined {
  return oldestDataAge(queryKeys.map(queryKey => queryClient.getQueryState(queryKey)))
}

/**
 * Re-runs the optimistic edit of every queued write, in order, so they show
 * on top of the cache. Edits are idempotent by contract. Conflict and
 * rejected items are not re-applied: the server's version is shown for them
 * until they are retried, overwritten or discarded.
 */
export function reapplyQueuedEdits(
  queryClient: QueryClient,
  items: readonly OutboxItem[],
  getDefinition: (key: string) => AnyOfflineMutationDef | undefined,
): void {
  for (const item of items) {
    if (item.status !== 'queued') continue
    const optimistic = getDefinition(item.key)?.optimistic
    if (!optimistic) continue
    try {
      editKeepingDataAge(queryClient, () => optimistic(item.vars, queryClient))
    }
    catch (error) {
      console.error(`Re-applying queued "${item.key}" change failed`, error)
    }
  }
}

/**
 * Keeps queued writes visible: whenever a query receives fresh data from the
 * server while writes are still queued, their optimistic edits are applied
 * again (batched to once per microtask, before observers re-render). Without
 * this, a refetch would hide a queued change until it is sent. Returns an
 * unsubscribe function.
 */
export function keepQueuedEditsVisible(
  queryClient: QueryClient,
  getItems: () => readonly OutboxItem[],
  getDefinition: (key: string) => AnyOfflineMutationDef | undefined,
): () => void {
  let scheduled = false
  return queryClient.getQueryCache().subscribe((event) => {
    if (event.type !== 'updated' || event.action.type !== 'success' || event.action.manual) return
    if (scheduled || !getItems().some(item => item.status === 'queued')) return
    scheduled = true
    queueMicrotask(() => {
      scheduled = false
      reapplyQueuedEdits(queryClient, getItems(), getDefinition)
    })
  })
}

export interface OptimisticEdit {
  /** Undo now (failed live send): restore the previous states and refetch them. */
  rollback: () => void
  /**
   * When the oldest data the edit changed was fetched (device ms): what the
   * write was based on. Undefined when it changed no cached data (only
   * created queries), which puts no bound on the write.
   */
  basedOn?: number
}

/**
 * Runs an optimistic cache edit and records how to undo it in memory.
 *
 * Rollback strategy: before the edit, keep a reference to every cached query's
 * state object (cheap: no copying). Query state is immutable — every change
 * replaces the object — so after the edit the touched queries are exactly
 * those whose state object changed, plus any query the edit created. Undoing
 * restores those previous state objects (and removes the created queries),
 * then invalidates the touched queries so anything that changed on the server
 * meanwhile is fetched again. This needs no knowledge of which keys a domain's
 * `optimistic` function edits, and leaves every other query alone.
 *
 * It relies on `optimistic` returning new objects from `setQueryData` updaters
 * rather than mutating the cached ones (see `OfflineMutationDef.optimistic`).
 */
export function runOptimistic(queryClient: QueryClient, edit: () => void): OptimisticEdit {
  const cache = queryClient.getQueryCache()
  const { before: previousStates, touched } = editKeepingDataAge(queryClient, edit)

  function restore(queries: Query[]) {
    for (const query of queries) {
      const previous = previousStates.get(query.queryHash)
      if (previous === undefined) cache.remove(query)
      else query.setState(previous)
    }
    return queries
  }

  let undone = false
  return {
    rollback() {
      if (undone) return
      undone = true
      const restored = restore(touched)
      const hashes = new Set(restored.filter(query => previousStates.has(query.queryHash)).map(query => query.queryHash))
      if (hashes.size > 0) {
        void queryClient.invalidateQueries({ predicate: query => hashes.has(query.queryHash) })
      }
    },
    basedOn: oldestDataAge(touched.map(query => previousStates.get(query.queryHash))),
  }
}

/** {@link runOptimistic}, returning only the rollback function. */
export function applyOptimistic(queryClient: QueryClient, edit: () => void): () => void {
  return runOptimistic(queryClient, edit).rollback
}

/**
 * Takes a discarded queued write off the screen without a server: the
 * definition's `revert` removes its edit by id, then the remaining queued
 * writes are re-applied on top (they may have edited the same queries since).
 * A definition without `revert` cannot be undone locally: everything is
 * refetched, so its edit goes once the server can be reached. Returns whether
 * the edit was taken back now.
 */
export function discardQueuedEdit(
  queryClient: QueryClient,
  discarded: Pick<OutboxItem, 'key' | 'vars'>,
  remaining: readonly OutboxItem[],
  getDefinition: (key: string) => AnyOfflineMutationDef | undefined,
): boolean {
  const revert = getDefinition(discarded.key)?.revert
  let reverted = false
  if (revert) {
    try {
      editKeepingDataAge(queryClient, () => revert(discarded.vars, queryClient))
      reverted = true
    }
    catch (error) {
      console.error(`Taking back discarded "${discarded.key}" change failed`, error)
    }
  }
  reapplyQueuedEdits(queryClient, remaining, getDefinition)
  void queryClient.invalidateQueries()
  return reverted
}

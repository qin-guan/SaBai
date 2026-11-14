import type { QueryClient } from '@tanstack/vue-query'
import type { AnyOfflineMutationDef } from './defineOfflineMutation'
import type { OutboxItem } from './outbox'

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
    try {
      getDefinition(item.key)?.optimistic?.(item.vars, queryClient)
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

/**
 * What a queued write's optimistic edit replaced, saved with the outbox item so
 * the edit can be taken back when the item is discarded, even offline and after
 * a reload: each touched query's key and its data before the edit (`existed:
 * false` for a query the edit created).
 */
export interface UndoEntry {
  queryKey: readonly unknown[]
  existed: boolean
  data?: unknown
}

export interface OptimisticEdit {
  /** Undo now (failed live send): restore the previous states and refetch them. */
  rollback: () => void
  /** The same as plain data, for undoing later (see {@link UndoEntry}). */
  undo: UndoEntry[]
}

/**
 * Runs an optimistic cache edit and records how to undo it.
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
  const before = new Map(cache.getAll().map(query => [query.queryHash, query.state]))

  try {
    edit()
  }
  catch (error) {
    restore()
    throw error
  }

  const touched = cache.getAll().filter(query => before.get(query.queryHash) !== query.state)

  function restore(queries = cache.getAll().filter(query => before.get(query.queryHash) !== query.state)) {
    for (const query of queries) {
      const previous = before.get(query.queryHash)
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
      const hashes = new Set(restored.filter(query => before.has(query.queryHash)).map(query => query.queryHash))
      if (hashes.size > 0) {
        void queryClient.invalidateQueries({ predicate: query => hashes.has(query.queryHash) })
      }
    },
    undo: touched.map((query) => {
      const previous = before.get(query.queryHash)
      return previous === undefined
        ? { queryKey: query.queryKey, existed: false }
        : { queryKey: query.queryKey, existed: true, data: previous.data }
    }),
  }
}

/** {@link runOptimistic}, returning only the rollback function. */
export function applyOptimistic(queryClient: QueryClient, edit: () => void): () => void {
  return runOptimistic(queryClient, edit).rollback
}

/**
 * Takes a discarded write's optimistic edit off the screen, without a server:
 * restores the queries it touched to their data from before the edit, then
 * re-applies the remaining queued writes' edits in order (they may have edited
 * the same queries since), then invalidates those queries so they are fetched
 * again when the server can be reached. Without an undo record (a write that
 * was never queued with one), it falls back to invalidating everything.
 */
export function undoQueuedEdit(
  queryClient: QueryClient,
  discarded: Pick<OutboxItem, 'undo'>,
  remaining: readonly OutboxItem[],
  getDefinition: (key: string) => AnyOfflineMutationDef | undefined,
): void {
  const undo = discarded.undo
  if (!undo) {
    void queryClient.invalidateQueries()
    return
  }
  for (const entry of undo) {
    // A query that had no data before the edit is removed rather than emptied.
    if (entry.existed && entry.data !== undefined) queryClient.setQueryData(entry.queryKey, entry.data)
    else queryClient.removeQueries({ queryKey: entry.queryKey, exact: true })
  }
  reapplyQueuedEdits(queryClient, remaining, getDefinition)
  for (const entry of undo) {
    if (entry.existed && entry.data !== undefined) void queryClient.invalidateQueries({ queryKey: entry.queryKey, exact: true })
  }
}

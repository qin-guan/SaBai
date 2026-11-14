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
 * Runs an optimistic cache edit and returns a function that undoes it.
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
export function applyOptimistic(queryClient: QueryClient, edit: () => void): () => void {
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
  return () => {
    if (undone) return
    undone = true
    const restored = restore(touched)
    const hashes = new Set(restored.filter(query => before.has(query.queryHash)).map(query => query.queryHash))
    if (hashes.size > 0) {
      void queryClient.invalidateQueries({ predicate: query => hashes.has(query.queryHash) })
    }
  }
}

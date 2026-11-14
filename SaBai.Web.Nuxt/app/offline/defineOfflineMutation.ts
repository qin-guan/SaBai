import type { QueryClient, QueryKey } from '@tanstack/vue-query'

/**
 * Offline-capable writes.
 *
 * Every write that must work without a connection is described once, at module
 * top level, with {@link defineOfflineMutation} in `app/offline/mutations/<domain>.ts`
 * and imported from `app/offline/mutations/index.ts`, which the API plugin loads
 * at app start. Defining it registers it by `key`, so a write that was queued
 * before a reload can still be sent after it. Components run it with
 * `useOfflineMutation(def)`.
 */

export interface OfflineMutationContext {
  queryClient: QueryClient
  /**
   * Extra request headers for the SDK call: `If-Unmodified-Since`, the server
   * time of the cached data the write was made from (absent when it was made
   * from no cached data). Pass them with `...requestOptions(ctx)`.
   */
  headers: Record<string, string>
  signal?: AbortSignal
  /**
   * True when sent from the outbox: the write was made earlier, so times in
   * `vars` (e.g. when a visit was opened) are real; a live send lets the
   * server use its own clock.
   */
  replay: boolean
}

export interface OfflineMutationDef<TVars, TResult> {
  /** Stable, unique key, e.g. 'vitals.upsert'. Persisted with the queued item; never rename casually. */
  key: string
  /** Short human label for the pending-changes list, e.g. `Vitals for ${vars.patientName}`. */
  describe: (vars: TVars) => string
  /**
   * The REST call. Must be re-runnable from `vars` alone (vars are
   * JSON-serialised into IndexedDB), so put everything it needs in `vars`:
   * ids (client-generated with `uuidv7()` for creates), the body, and any
   * names `describe` shows.
   */
  send: (vars: TVars, ctx: OfflineMutationContext) => Promise<TResult>
  /**
   * Edit the query cache so the UI shows the change before the server confirms
   * it. Runs when the write is made (live or queued) and again after any
   * server refetch while the write is still queued, so the queued change stays
   * visible on top of fresh data. It must therefore be:
   * - safe when the cached queries are absent (`setQueryData(key, old => old && …)`),
   * - idempotent (upsert/replace/remove by id, never blindly append),
   * - immutable (return new objects; never mutate `old`), so a failed live
   *   send can restore the previous cache entries.
   */
  optimistic?: (vars: TVars, queryClient: QueryClient) => void
  /**
   * Runs after the server accepted the write (live or replayed): write the
   * server response into the cache and/or invalidate queries.
   */
  onSuccess?: (result: TResult, vars: TVars, queryClient: QueryClient) => void | Promise<void>
  /**
   * Ids of the records this write creates, changes or deletes. When a queued
   * write ends up in conflict or is rejected, later queued writes for the same
   * records, or that depend on one of them, wait behind it.
   */
  recordIds?: (vars: TVars) => string[]
  /**
   * True when the write creates the records in `recordIds` (a registration, a
   * new consult). Writes that `dependsOn` one of them wait while it is failed.
   */
  creates?: boolean
  /**
   * Ids of records that must exist on the server before this write can succeed
   * (the patient and visit of a vitals record, the consult of a referral). It
   * waits behind a failed write that creates one of them (a rejected
   * registration), but not behind a failed change to one (a conflicted patient
   * edit) or to a sibling (a rejected vitals does not hold back the same
   * visit's eyesight).
   */
  dependsOn?: (vars: TVars) => string[]
  /**
   * The cached queries the write is made from — what the form showed (e.g. the
   * patient detail for a patient edit). The write is checked against changes
   * made after the oldest of them was fetched. Default: every cached query the
   * optimistic edit changes.
   */
  baseQueries?: (vars: TVars) => QueryKey[]
  /**
   * Takes this write's optimistic edit off the cache by id (e.g. removes a
   * created record from the lists), for discarding a queued write while
   * offline. Without it, a discarded write stays on screen until the next
   * successful refetch.
   */
  revert?: (vars: TVars, queryClient: QueryClient) => void
}

// Registered definitions are stored without their type parameters.
export type AnyOfflineMutationDef = OfflineMutationDef<any, any>

const registry = new Map<string, AnyOfflineMutationDef>()

/**
 * Declares an offline-capable write and registers it under `def.key`.
 * Call it at module top level only. Registering the same key again replaces
 * the earlier definition (module hot reload re-runs the module).
 */
export function defineOfflineMutation<TVars, TResult>(
  def: OfflineMutationDef<TVars, TResult>,
): OfflineMutationDef<TVars, TResult> {
  if (!def.key) throw new Error('An offline mutation needs a non-empty key.')
  registry.set(def.key, def)
  return def
}

/** The registered definition for a queued item's key, if any. */
export function getOfflineMutation(key: string): AnyOfflineMutationDef | undefined {
  return registry.get(key)
}

/** Keys of all registered definitions (for diagnostics and tests). */
export function listOfflineMutationKeys(): string[] {
  return [...registry.keys()]
}

/**
 * The per-call options that carry an offline mutation's context into an SDK
 * call: the abort signal, and the extra headers (`If-Unmodified-Since` on
 * replay). The headers travel in `options.headers`, which the API transport
 * (`app/offline/transport.ts`) merges into the request, because the generated
 * SDK types do not allow free-form `headers`.
 *
 * ```ts
 * send: (vars, ctx) => saBaiAPIEndpointsVillageCodesUpdatePatchEndpoint({
 *   ...apiConfig, ...requestOptions(ctx), path: { id: vars.id }, body: vars.body,
 * }).unwrap(),
 * ```
 */
export function requestOptions(ctx: Pick<OfflineMutationContext, 'headers' | 'signal'>): {
  signal?: AbortSignal
  options: { headers: Record<string, string> }
} {
  return { signal: ctx.signal, options: { headers: { ...ctx.headers } } }
}

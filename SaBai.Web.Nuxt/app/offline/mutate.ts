import type { QueryClient } from '@tanstack/vue-query'
import type { OfflineMutationDef } from './defineOfflineMutation'
import { isNetworkError } from './network'
import { cachedDataAge, runOptimistic, type OptimisticEdit } from './optimistic'
import type { Outbox } from './outbox'
import { getApiErrorStatus } from '~/utils/apiErrors'

export type OfflineMutationOutcome<TResult> =
  | { queued: false, result: TResult } // server accepted it now
  | { queued: true } // saved on this device, will be sent later

export interface OfflineMutationRuntime {
  queryClient: QueryClient
  outbox: Outbox
  /** Whether requests are currently failing to reach the server. */
  isOffline: () => boolean
  /** A request just failed for network reasons. */
  reportNetworkFailure?: () => void
  /** Called after an item was queued while the device looks online, to send it soon. */
  scheduleReplay?: () => void
  /** Converts a device time to the server's clock as an HTTP-date (see serverTime.ts). */
  serverTimeAt?: (deviceMs: number) => string | undefined
  /** A live send got 401: the session ended (the write was queued for after sign-in). */
  onUnauthorized?: () => void
}

/** Shown when a live save hits a change someone else made since the data was loaded. */
export const LIVE_CONFLICT_MESSAGE
  = 'Not saved: someone else changed this record after it was loaded here. Their version has been reloaded; check it and save again.'

/**
 * The server time the write is based on: when the data it was made from was
 * fetched — the definition's `baseQueries` when one of them is cached,
 * otherwise the oldest data the optimistic edit changed. Undefined (no
 * `If-Unmodified-Since`) when the write was made from no cached data.
 */
function baseTimeOf(edit: OptimisticEdit, baseQueriesAge: number | undefined, runtime: OfflineMutationRuntime): string | undefined {
  const basedOn = baseQueriesAge ?? edit.basedOn
  return basedOn === undefined ? undefined : runtime.serverTimeAt?.(basedOn)
}

/**
 * Makes an offline-capable write:
 * 1. runs `optimistic`, noting how old the cached data it is based on is;
 * 2. queues it when the device is offline, or when earlier writes are still
 *    queued (order is kept) or waiting for a record it changes or needs;
 * 3. otherwise sends it now, with `If-Unmodified-Since` set to that data's
 *    server time. Success → `onSuccess`, `{ queued: false, result }`.
 *    A network failure → queued. 401 → queued and the user is sent to sign in.
 *    412 → the optimistic edit is undone, the data refetched, and an error
 *    saying so is thrown. Any other error (the server answered) → the
 *    optimistic edit is undone and the error is thrown, so forms show
 *    validation messages as usual.
 */
export async function runOfflineMutation<TVars, TResult>(
  def: OfflineMutationDef<TVars, TResult>,
  vars: TVars,
  runtime: OfflineMutationRuntime,
): Promise<OfflineMutationOutcome<TResult>> {
  const { queryClient, outbox } = runtime
  const baseQueriesAge = def.baseQueries ? cachedDataAge(queryClient, def.baseQueries(vars)) : undefined
  const optimistic = def.optimistic
  const edit: OptimisticEdit = optimistic
    ? runOptimistic(queryClient, () => optimistic(vars, queryClient))
    : { rollback: () => {} }
  const rollback = edit.rollback
  const baseTime = baseTimeOf(edit, baseQueriesAge, runtime)

  const queue = async (): Promise<OfflineMutationOutcome<TResult>> => {
    try {
      await outbox.enqueue(def, vars, { baseTime })
    }
    catch (error) {
      // Could not even save it on this device (storage full or blocked).
      rollback()
      throw error
    }
    if (!runtime.isOffline()) runtime.scheduleReplay?.()
    return { queued: true }
  }

  if (runtime.isOffline() || outbox.mustQueue({ recordIds: def.recordIds?.(vars), dependsOn: def.dependsOn?.(vars) })) {
    return queue()
  }

  let result: TResult
  try {
    const headers: Record<string, string> = baseTime ? { 'If-Unmodified-Since': baseTime } : {}
    result = await def.send(vars, { queryClient, headers, replay: false })
  }
  catch (error) {
    if (isNetworkError(error)) {
      runtime.reportNetworkFailure?.()
      return queue()
    }
    const status = getApiErrorStatus(error)
    if (status === 401) {
      // Signed out meanwhile: keep the write for when they sign in again.
      const outcome = await queue()
      runtime.onUnauthorized?.()
      return outcome
    }
    // Restores the cache and refetches what the edit touched.
    rollback()
    if (status === 412) {
      for (const queryKey of def.baseQueries?.(vars) ?? []) void queryClient.invalidateQueries({ queryKey })
      throw Object.assign(new Error(LIVE_CONFLICT_MESSAGE), {
        name: 'ConflictError',
        status,
        data: { detail: LIVE_CONFLICT_MESSAGE },
        cause: error,
      })
    }
    throw error
  }

  try {
    await def.onSuccess?.(result, vars, queryClient)
  }
  catch (error) {
    // The server has the change; report the cache problem without failing the save.
    console.error(`Offline mutation "${def.key}" onSuccess failed`, error)
  }
  return { queued: false, result }
}

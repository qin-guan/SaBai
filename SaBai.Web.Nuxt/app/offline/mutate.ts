import type { QueryClient } from '@tanstack/vue-query'
import type { OfflineMutationDef } from './defineOfflineMutation'
import { isNetworkError } from './network'
import { runOptimistic, type OptimisticEdit } from './optimistic'
import type { Outbox } from './outbox'

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
}

/**
 * Makes an offline-capable write:
 * 1. runs `optimistic`;
 * 2. queues it when the device is offline, or when earlier writes are still
 *    queued (order is kept) or waiting for the same record;
 * 3. otherwise sends it now. Success → `onSuccess`, `{ queued: false, result }`.
 *    A network failure → queued. Any other error (the server answered) → the
 *    optimistic edit is undone and the error is thrown, so forms show
 *    validation messages as usual.
 */
export async function runOfflineMutation<TVars, TResult>(
  def: OfflineMutationDef<TVars, TResult>,
  vars: TVars,
  runtime: OfflineMutationRuntime,
): Promise<OfflineMutationOutcome<TResult>> {
  const { queryClient, outbox } = runtime
  const optimistic = def.optimistic
  const edit: OptimisticEdit = optimistic
    ? runOptimistic(queryClient, () => optimistic(vars, queryClient))
    : { rollback: () => {}, undo: [] }
  const rollback = edit.rollback

  const queue = async (): Promise<OfflineMutationOutcome<TResult>> => {
    try {
      // The undo record lets a later discard take the edit back offline.
      await outbox.enqueue(def, vars, { undo: edit.undo })
    }
    catch (error) {
      // Could not even save it on this device (storage full or blocked).
      rollback()
      throw error
    }
    if (!runtime.isOffline()) runtime.scheduleReplay?.()
    return { queued: true }
  }

  if (runtime.isOffline() || outbox.mustQueue(def.recordIds?.(vars))) {
    return queue()
  }

  let result: TResult
  try {
    result = await def.send(vars, { queryClient, headers: {} })
  }
  catch (error) {
    if (isNetworkError(error)) {
      runtime.reportNetworkFailure?.()
      return queue()
    }
    rollback()
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

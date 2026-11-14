import { toast } from 'vue-sonner'
import type { OfflineMutationOutcome } from '~/offline/mutate'
import { isNetworkError } from '~/offline/network'
import { getApiErrorMessage } from '~/utils/apiErrors'

/**
 * The public offline API, auto-imported from here (app/offline itself is not
 * an auto-import directory).
 */
export { defineOfflineMutation, requestOptions } from '~/offline/defineOfflineMutation'
export type { OfflineMutationContext, OfflineMutationDef } from '~/offline/defineOfflineMutation'
export { isNetworkError }
export { registerOfflinePrefetch } from '~/offline/prefetch'

export const SAVED_ON_DEVICE_MESSAGE = 'Saved on this device. It will be sent when the connection is back.'

/**
 * The one way forms report an offline mutation's outcome, so every screen
 * says the same thing: the success message when the server accepted it, or
 * "saved on this device" when it was queued.
 *
 * ```ts
 * const outcome = await mutateAsync(vars)
 * notifySaved(outcome, 'Vitals saved.')
 * ```
 */
export function notifySaved(outcome: OfflineMutationOutcome<unknown>, successMessage: string): void {
  if (outcome.queued) toast.info(SAVED_ON_DEVICE_MESSAGE)
  else toast.success(successMessage)
}

/**
 * Message for a failed read: "needs a connection" when the device is offline,
 * otherwise the API's message. For online-only features and error states.
 */
export function describeLoadError(error: unknown, fallback = 'Could not load this.'): string {
  return isNetworkError(error)
    ? 'This needs a connection to the server. Try again when you are back online.'
    : getApiErrorMessage(error, fallback)
}

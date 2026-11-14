import type { Ref } from 'vue'
import type { OfflineMutationDef } from '~/offline/defineOfflineMutation'
import type { OfflineMutationOutcome } from '~/offline/mutate'
import { mutateOffline } from '~/offline/runtime'

export type { OfflineMutationOutcome } from '~/offline/mutate'

/**
 * Runs an offline-capable write (see `defineOfflineMutation`).
 *
 * `mutateAsync(vars)` applies the optimistic cache edit, then either sends the
 * write now — resolving `{ queued: false, result }` — or saves it on this
 * device for later — resolving `{ queued: true }` — when the device is offline,
 * the request cannot reach the server, or earlier writes are still waiting.
 * When the server answers with an error (validation, 403, 409…) the
 * optimistic edit is undone and the error is thrown, as with `useMutation`.
 * Show the outcome with `notifySaved(outcome, '… saved')`.
 */
export function useOfflineMutation<TVars, TResult>(def: OfflineMutationDef<TVars, TResult>): {
  mutateAsync: (vars: TVars) => Promise<OfflineMutationOutcome<TResult>>
  isPending: Ref<boolean>
} {
  const running = ref(0)
  const isPending = computed(() => running.value > 0)

  async function mutateAsync(vars: TVars): Promise<OfflineMutationOutcome<TResult>> {
    running.value++
    try {
      return await mutateOffline(def, vars)
    }
    finally {
      running.value--
    }
  }

  return { mutateAsync, isPending }
}

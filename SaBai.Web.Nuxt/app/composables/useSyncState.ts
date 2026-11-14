import type { OutboxItem } from '~/offline/outbox'
import { getPrefetchState, subscribePrefetch, type PrefetchState } from '~/offline/prefetch'
import {
  clearOfflineData,
  downloadForOffline,
  getOfflineRuntime,
  lastServerContactAt,
  syncNow as runSyncNow,
} from '~/offline/runtime'

/**
 * Reactive view of the offline state of this device, shared by the header
 * badge and the sync page: connection, queued writes and the ones that need
 * a person (conflict or rejected), replay progress and offline downloads.
 */

const items = shallowRef<readonly OutboxItem[]>([])
const replaying = ref(false)
const isOffline = ref(false)
const lastSyncedAt = ref<Date | null>(null)
const prefetch = shallowRef<PrefetchState>(getPrefetchState())
let bound = false

function refreshLastSynced() {
  // "Synced" means the server was reached and nothing is waiting to be sent.
  const contact = lastServerContactAt()
  if (contact !== undefined && !items.value.some(item => item.status === 'queued')) {
    lastSyncedAt.value = new Date(contact)
  }
}

function bind() {
  if (bound || !import.meta.client) return
  const { outbox, connectivity } = getOfflineRuntime()
  bound = true
  const syncOutbox = () => {
    items.value = outbox.getItems()
    replaying.value = outbox.isReplaying()
    refreshLastSynced()
  }
  syncOutbox()
  outbox.subscribe(syncOutbox)
  isOffline.value = connectivity.getState().offline
  connectivity.subscribe((state) => {
    isOffline.value = state.offline
    refreshLastSynced()
  })
  subscribePrefetch((state) => {
    prefetch.value = state
    refreshLastSynced()
  })
  // Every successful request counts as contact; pick that up without an event per request.
  setInterval(refreshLastSynced, 10_000)
}

export function useSyncState() {
  bind()
  const { outbox } = import.meta.client ? getOfflineRuntime() : { outbox: undefined }

  const pendingCount = computed(() => items.value.filter(item => item.status === 'queued').length)
  const attentionCount = computed(() => items.value.filter(item => item.status !== 'queued').length)

  const statusLabel = computed(() => {
    if (attentionCount.value > 0) return `${attentionCount.value} need${attentionCount.value === 1 ? 's' : ''} attention`
    if (isOffline.value) return pendingCount.value > 0 ? `Offline · ${pendingCount.value} pending` : 'Offline'
    if (replaying.value) return 'Syncing…'
    if (pendingCount.value > 0) return `${pendingCount.value} pending`
    return 'All synced'
  })

  return {
    /** Requests are failing to reach the server (or the browser reports no network). */
    isOffline: readonly(isOffline),
    isOnline: computed(() => !isOffline.value),
    /** Writes waiting to be sent, including ones held behind a conflict. */
    pendingCount,
    /** Conflict + rejected writes: someone has to retry, overwrite or discard them. */
    attentionCount,
    replaying: readonly(replaying),
    /** When the server was last reached with nothing left to send. */
    lastSyncedAt: readonly(lastSyncedAt),
    /** Every outbox item, in the order it will be sent. */
    items: computed(() => items.value),
    statusLabel,
    /** Offline download progress. */
    prefetch: readonly(prefetch),
    /** Checks the connection, sends queued writes and refreshes data. Resolves whether the server was reached. */
    syncNow: runSyncNow,
    downloadForOffline,
    retry: (id: string) => outbox!.retry(id).then(() => runSyncNow()),
    overwrite: (id: string) => outbox!.overwrite(id).then(() => runSyncNow()),
    discard: async (id: string) => {
      await outbox!.discard(id)
      // Undo the discarded change's optimistic edit wherever the server can be reached.
      await getOfflineRuntime().queryClient.invalidateQueries()
    },
    clearOfflineData,
  }
}

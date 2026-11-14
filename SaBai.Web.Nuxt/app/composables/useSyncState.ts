import type { OutboxItem } from '~/offline/outbox'
import { getPrefetchState, subscribePrefetch, type PrefetchState } from '~/offline/prefetch'
import {
  clearOfflineData,
  discardOutboxItem,
  downloadForOffline,
  getOfflineRuntime,
  getOfflineUser,
  lastServerContactAt,
  requeueOutboxItem,
  retryAllOutboxItems,
  subscribeOfflineUser,
  syncNow as runSyncNow,
} from '~/offline/runtime'

/**
 * Reactive view of the offline state of this device, shared by the header
 * badge and the sync page: connection, the signed-in user's queued writes and
 * the ones that need a person (conflict or rejected), other users' parked
 * writes, replay progress and offline downloads.
 */

const items = shallowRef<readonly OutboxItem[]>([])
/** Changes on sign-in/out, so own/parked are recomputed. */
const userId = ref<string | undefined>(undefined)
const replaying = ref(false)
const isOffline = ref(false)
const lastSyncedAt = ref<Date | null>(null)
const prefetch = shallowRef<PrefetchState>(getPrefetchState())
let bound = false

function isOwnItem(item: OutboxItem): boolean {
  void userId.value // reactive dependency
  return import.meta.client ? getOfflineRuntime().outbox.isOwn(item) : false
}

function refreshLastSynced() {
  // "Synced" means the server was reached and none of the user's writes is waiting.
  const contact = lastServerContactAt()
  if (contact !== undefined && !items.value.some(item => item.status === 'queued' && isOwnItem(item))) {
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
  userId.value = getOfflineUser()?.id
  subscribeOfflineUser((user) => {
    userId.value = user?.id
  })
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

  const ownItems = computed(() => items.value.filter(isOwnItem))
  const parkedItems = computed(() => items.value.filter(item => !isOwnItem(item)))
  const pendingCount = computed(() => ownItems.value.filter(item => item.status === 'queued').length)
  const attentionCount = computed(() =>
    ownItems.value.filter(item => item.status !== 'queued').length + parkedItems.value.length)

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
    /** The signed-in user's writes waiting to be sent, including ones held behind a conflict. */
    pendingCount,
    /** The user's conflict + rejected writes, plus other users' parked writes: someone has to act. */
    attentionCount,
    replaying: readonly(replaying),
    /** When the server was last reached with none of the user's writes left to send. */
    lastSyncedAt: readonly(lastSyncedAt),
    /** Every outbox item (every user's), in order. */
    items: computed(() => items.value),
    /** The signed-in user's items, in the order they will be sent. */
    ownItems,
    /** Other users' items, kept until they sign in again on this device. */
    parkedItems,
    statusLabel,
    /** Offline download progress. */
    prefetch: readonly(prefetch),
    /** Checks the connection, sends queued writes and refreshes data. Resolves whether the server was reached. */
    syncNow: runSyncNow,
    downloadForOffline,
    retry: (id: string) => requeueOutboxItem(id),
    overwrite: (id: string) => requeueOutboxItem(id, { overwrite: true }),
    /** Retries every conflict and rejected item of the user, then syncs. */
    retryAll: retryAllOutboxItems,
    /**
     * Drops an item (the change is lost). Resolves whether its edit left the
     * screen at once; otherwise it goes with the next refetch from the server.
     */
    discard: (id: string) => discardOutboxItem(id),
    clearOfflineData,
  }
}

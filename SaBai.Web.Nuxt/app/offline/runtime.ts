import type { QueryClient } from '@tanstack/vue-query'
import { onlineManager } from '@tanstack/vue-query'
import { uuidv7 } from '~/utils/uuid'
import { createConnectivity, probeApiHealth, type Connectivity } from './connectivity'
import { getOfflineMutation, type OfflineMutationDef } from './defineOfflineMutation'
import { runOfflineMutation, type OfflineMutationOutcome } from './mutate'
import { keepQueuedEditsVisible, reapplyQueuedEdits } from './optimistic'
import { createOutbox, type Outbox, type OutboxItem } from './outbox'
import { maybeRunOfflinePrefetch, resetPrefetchState, runOfflinePrefetch } from './prefetch'
import { clearServerTime, getBaseTime, getLastServerContactAt, recordServerDate } from './serverTime'
import { idbStore, memoryStore } from './storage'
import { setTransportHooks } from './transport'

/**
 * The offline runtime of this tab: the outbox, connectivity and the glue
 * between them, the query client and the API transport. Created once by the
 * API plugin at start-up (`initOfflineRuntime`); composables reach it with
 * `getOfflineRuntime()`.
 */

export interface OfflineRuntime {
  queryClient: QueryClient
  outbox: Outbox
  connectivity: Connectivity
}

let runtime: OfflineRuntime | undefined
let clearPersistedQueries: () => Promise<void> = async () => {}

const LOCK_NAME = 'sabai-outbox-replay'
const CHANNEL_NAME = 'sabai-outbox'

function createExclusiveLock() {
  const locks = typeof navigator !== 'undefined' ? navigator.locks : undefined
  if (!locks) return undefined
  return <T>(fn: () => Promise<T>) =>
    locks.request(LOCK_NAME, { ifAvailable: true }, lock => (lock ? fn() : undefined)) as Promise<T | undefined>
}

export async function initOfflineRuntime(options: {
  queryClient: QueryClient
  /** Removes the persisted query cache (sign-out). */
  clearPersistedQueries: () => Promise<void>
}): Promise<OfflineRuntime> {
  if (runtime) return runtime
  const { queryClient } = options
  clearPersistedQueries = options.clearPersistedQueries

  // Ask the browser not to evict the cache and outbox under storage pressure.
  void navigator.storage?.persist?.().catch(() => {})

  const channel = typeof BroadcastChannel !== 'undefined' ? new BroadcastChannel(CHANNEL_NAME) : undefined
  const outbox = createOutbox({
    store: typeof indexedDB !== 'undefined' ? idbStore<OutboxItem>('sabai-outbox', 'items') : memoryStore<OutboxItem>(),
    getDefinition: getOfflineMutation,
    getQueryClient: () => queryClient,
    getBaseTime,
    newId: uuidv7,
    withExclusiveLock: createExclusiveLock(),
    notifyOtherTabs: () => channel?.postMessage('changed'),
  })
  channel?.addEventListener('message', () => void outbox.load())

  const connectivity = createConnectivity({
    probe: () => probeApiHealth(),
    shouldProbeWhileOnline: () => outbox.sendableCount() > 0,
    onReachable: () => {
      if (outbox.sendableCount() > 0) void outbox.replay()
    },
    initialOffline: typeof navigator !== 'undefined' && navigator.onLine === false,
  })

  setTransportHooks({
    onReachable: connectivity.reportReachable,
    onNetworkFailure: connectivity.reportNetworkFailure,
    onServerDate: date => recordServerDate(date),
  })

  // Vue Query pauses retries and refetches on reconnect from the same signal.
  onlineManager.setEventListener(setOnline => connectivity.subscribe(state => setOnline(!state.offline)))
  onlineManager.setOnline(!connectivity.getState().offline)

  try {
    await outbox.load()
  }
  catch (error) {
    console.error('Could not read the offline outbox', error)
  }
  // Show queued writes on top of the restored cache, and keep them there.
  reapplyQueuedEdits(queryClient, outbox.getItems(), getOfflineMutation)
  keepQueuedEditsVisible(queryClient, outbox.getItems, getOfflineMutation)

  connectivity.start()
  runtime = { queryClient, outbox, connectivity }
  return runtime
}

export function getOfflineRuntime(): OfflineRuntime {
  if (!runtime) throw new Error('The offline runtime has not been started (see plugins/api.ts).')
  return runtime
}

/** Runs an offline mutation against this tab's runtime (used by `useOfflineMutation`). */
export function mutateOffline<TVars, TResult>(
  def: OfflineMutationDef<TVars, TResult>,
  vars: TVars,
): Promise<OfflineMutationOutcome<TResult>> {
  const { queryClient, outbox, connectivity } = getOfflineRuntime()
  return runOfflineMutation(def, vars, {
    queryClient,
    outbox,
    isOffline: () => connectivity.getState().offline,
    reportNetworkFailure: connectivity.reportNetworkFailure,
    scheduleReplay: () => void outbox.replay(),
  })
}

/**
 * After sign-in (called on every authenticated navigation; cheap when there
 * is nothing to do): sends queued writes and, while online, downloads the
 * offline working set if it has not been downloaded recently.
 */
export function startOfflineSync(): void {
  if (!runtime) return
  const { outbox, connectivity, queryClient } = runtime
  if (connectivity.getState().offline) return
  if (outbox.sendableCount() > 0 && !outbox.isReplaying()) void outbox.replay()
  void maybeRunOfflinePrefetch(queryClient)
}

/**
 * "Sync now": checks the connection, sends queued writes, then refreshes the
 * screens. Resolves whether the server was reached.
 */
export async function syncNow(): Promise<boolean> {
  const { outbox, connectivity, queryClient } = getOfflineRuntime()
  const reachable = await connectivity.probeNow()
  if (!reachable) return false
  const result = await outbox.replay()
  // A run that sent something already invalidated everything.
  if (result.sent + result.failed === 0 && !result.stopped) await queryClient.invalidateQueries()
  return true
}

/** "Download for offline use". */
export function downloadForOffline() {
  return runOfflinePrefetch(getOfflineRuntime().queryClient)
}

/** Device time (ms) of the last successful API response. */
export function lastServerContactAt(): number | undefined {
  return getLastServerContactAt()
}

/**
 * Removes everything this device keeps for the signed-in user: queued writes,
 * the cached query data (memory and IndexedDB), the server time and the
 * download timestamp. Used on sign-out; queued writes are lost.
 */
export async function clearOfflineData(): Promise<void> {
  if (runtime) await runtime.outbox.clear()
  await clearPersistedQueries()
  clearServerTime()
  resetPrefetchState()
}

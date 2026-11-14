import type { QueryClient } from '@tanstack/vue-query'
import { onlineManager } from '@tanstack/vue-query'
import { uuidv7 } from '~/utils/uuid'
import { createConnectivity, probeApiHealth, type Connectivity } from './connectivity'
import { getOfflineMutation, type OfflineMutationDef } from './defineOfflineMutation'
import { runOfflineMutation, type OfflineMutationOutcome } from './mutate'
import { claimQueryCache, releaseQueryCache } from './cacheOwner'
import { keepQueuedEditsVisible, reapplyQueuedEdits, undoQueuedEdit } from './optimistic'
import { createOutbox, type Outbox, type OutboxItem, type OutboxUser, type ReplayResult } from './outbox'
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
let onUnauthorized: () => void = () => {}

/** The signed-in staff user, as far as the offline layer knows (see `setOfflineUser`). */
let currentUser: OutboxUser | undefined
const userListeners = new Set<(user: OutboxUser | undefined) => void>()

// The session query survives a change of user: it is how the new user is known.
const SESSION_QUERY_KEY = JSON.stringify(['auth', 'me'])
const isSessionQuery = (queryKey: readonly unknown[]) => JSON.stringify(queryKey) === SESSION_QUERY_KEY

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
  /** A replayed write got 401: the session ended (send the user to sign in). */
  onUnauthorized?: () => void
}): Promise<OfflineRuntime> {
  if (runtime) return runtime
  const { queryClient } = options
  clearPersistedQueries = options.clearPersistedQueries
  onUnauthorized = options.onUnauthorized ?? onUnauthorized

  // Ask the browser not to evict the cache and outbox under storage pressure.
  void navigator.storage?.persist?.().catch(() => {})

  const channel = typeof BroadcastChannel !== 'undefined' ? new BroadcastChannel(CHANNEL_NAME) : undefined
  const outbox = createOutbox({
    store: typeof indexedDB !== 'undefined' ? idbStore<OutboxItem>('sabai-outbox', 'items') : memoryStore<OutboxItem>(),
    getDefinition: getOfflineMutation,
    getQueryClient: () => queryClient,
    getBaseTime,
    getCurrentUser: () => currentUser,
    newId: uuidv7,
    withExclusiveLock: createExclusiveLock(),
    notifyOtherTabs: () => channel?.postMessage('changed'),
  })
  channel?.addEventListener('message', () => void outbox.load())

  const connectivity = createConnectivity({
    probe: () => probeApiHealth(),
    shouldProbeWhileOnline: () => outbox.sendableCount() > 0,
    onReachable: () => {
      if (outbox.sendableCount() > 0) void replayOutbox()
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
  // Keep the signed-in user's queued writes visible on top of refetched data.
  // (They are first applied when the user is known: `setOfflineUser`.)
  keepQueuedEditsVisible(queryClient, ownOutboxItems, getOfflineMutation)

  connectivity.start()
  runtime = { queryClient, outbox, connectivity }
  return runtime
}

export function getOfflineRuntime(): OfflineRuntime {
  if (!runtime) throw new Error('The offline runtime has not been started (see plugins/api.ts).')
  return runtime
}

/** The signed-in user's outbox items (all statuses), in order. */
export function ownOutboxItems(): OutboxItem[] {
  if (!runtime) return []
  const { outbox } = runtime
  return outbox.getItems().filter(outbox.isOwn)
}

export function getOfflineUser(): OutboxUser | undefined {
  return currentUser
}

export function subscribeOfflineUser(listener: (user: OutboxUser | undefined) => void): () => void {
  userListeners.add(listener)
  return () => userListeners.delete(listener)
}

/**
 * Tells the offline layer who is signed in (null: nobody). Only that user's
 * queued writes are sent and shown. When a different user than the owner of
 * the cached data signs in, the cached data is dropped; the other user's
 * queued writes stay parked until they sign in again.
 */
export function setOfflineUser(user: OutboxUser | null): void {
  const previous = currentUser?.id
  currentUser = user ?? undefined
  if (previous === currentUser?.id) return
  if (user && runtime) {
    claimQueryCache(runtime.queryClient, user.id, isSessionQuery)
    reapplyQueuedEdits(runtime.queryClient, ownOutboxItems(), getOfflineMutation)
  }
  for (const listener of userListeners) listener(currentUser)
}

/** Replays the outbox; a 401 means the session ended, so the user is sent to sign in. */
function replayOutbox(): Promise<ReplayResult> {
  return getOfflineRuntime().outbox.replay().then((result) => {
    if (result.stopped === 'unauthorized') onUnauthorized()
    return result
  })
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
    scheduleReplay: () => void replayOutbox(),
  })
}

/**
 * After sign-in (called on every authenticated navigation; cheap when there
 * is nothing to do): records who is signed in, sends their queued writes and,
 * while online, downloads the offline working set if it has not been
 * downloaded recently.
 */
export function startOfflineSync(user: OutboxUser): void {
  if (!runtime) return
  setOfflineUser(user)
  const { outbox, connectivity, queryClient } = runtime
  if (connectivity.getState().offline) return
  if (outbox.sendableCount() > 0 && !outbox.isReplaying()) void replayOutbox()
  void maybeRunOfflinePrefetch(queryClient)
}

/**
 * "Sync now": checks the connection, sends queued writes, then refreshes the
 * screens. Resolves whether the server was reached.
 */
export async function syncNow(): Promise<boolean> {
  const { connectivity, queryClient } = getOfflineRuntime()
  const reachable = await connectivity.probeNow()
  if (!reachable) return false
  const result = await replayOutbox()
  // A run that sent something already invalidated everything.
  if (result.sent + result.failed === 0 && !result.stopped) await queryClient.invalidateQueries()
  return true
}

/** Queues a conflict/rejected item again (with or without `If-Unmodified-Since`), shows it, and syncs. */
export async function requeueOutboxItem(id: string, { overwrite = false } = {}): Promise<boolean> {
  const { outbox, queryClient } = getOfflineRuntime()
  await (overwrite ? outbox.overwrite(id) : outbox.retry(id))
  reapplyQueuedEdits(queryClient, ownOutboxItems(), getOfflineMutation)
  return syncNow()
}

/**
 * Discards an item: the change is lost. A queued item's optimistic edit is
 * taken off the screen straight away, also offline (see `undoQueuedEdit`).
 * Conflict and rejected items already show the server's data.
 */
export async function discardOutboxItem(id: string): Promise<void> {
  const { outbox, queryClient } = getOfflineRuntime()
  const item = outbox.getItems().find(candidate => candidate.id === id)
  await outbox.discard(id)
  if (!item) return
  if (item.status === 'queued' && outbox.isOwn(item)) {
    undoQueuedEdit(queryClient, item, ownOutboxItems(), getOfflineMutation)
  }
  else if (outbox.isOwn(item)) {
    void queryClient.invalidateQueries()
  }
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
 * Sign-out: removes everything this device keeps for the signed-in user —
 * their queued writes, the cached query data (memory and IndexedDB), the
 * server time and the download timestamp. Their queued writes are lost.
 * Other users' parked writes stay until those users sign in again.
 */
export async function clearOfflineData(): Promise<void> {
  if (runtime) await runtime.outbox.clear(runtime.outbox.isOwn)
  await clearPersistedQueries()
  clearServerTime()
  resetPrefetchState()
  releaseQueryCache()
  setOfflineUser(null)
}

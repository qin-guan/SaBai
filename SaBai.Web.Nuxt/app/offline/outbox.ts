import type { QueryClient } from '@tanstack/vue-query'
import type { AnyOfflineMutationDef, OfflineMutationDef } from './defineOfflineMutation'
import { classifyReplayError, describeWriteError } from './network'
import { getApiErrorStatus } from '~/utils/apiErrors'
import type { KeyValueStore } from './storage'

/**
 * The outbox: writes that could not be sent yet, persisted in IndexedDB and
 * replayed strictly in order, one at a time.
 *
 * Replay of one item:
 * - success → the definition's `onSuccess` runs and the item is removed;
 * - network failure / timeout / 401 / 408 / 429 → the run stops, everything
 *   stays queued in order and is tried again later;
 * - a server error (5xx other than a gateway error) → the run stops too and
 *   the item is tried again later; after {@link MAX_SERVER_ERRORS} runs that
 *   each failed on it this way it is marked `rejected`, so one write the server
 *   cannot handle does not hold up the queue for ever;
 * - 412 → the item is marked `conflict`;
 * - any other error → the item is marked `rejected` with the server's message.
 *
 * A `conflict` or `rejected` item does not hold up the rest of the outbox,
 * except later items for the same record or for a record it creates: a later
 * item whose `recordIds` or `dependsOn` share an id with the failed item's
 * `recordIds` stays queued behind it until it is retried, overwritten or
 * discarded (and holds back items behind it in turn). After a run that sent
 * anything and was not stopped, all queries are invalidated so the cache
 * matches the server.
 *
 * Every item belongs to the staff user who made it (`userId`). Only the
 * signed-in user's items are sent; nothing is sent while nobody is signed in.
 * Another user's items are "parked": kept, not sent, and they hold back the
 * current user's items only for the same records.
 */

export type OutboxStatus = 'queued' | 'conflict' | 'rejected'

export interface OutboxItem {
  id: string
  /** Position in the outbox: strictly increasing in the order items were queued. */
  seq: number
  /** The `OfflineMutationDef.key` that sends it. */
  key: string
  /** The mutation's variables, as JSON-compatible data. */
  vars: unknown
  /** `describe(vars)` at the time it was queued. */
  label: string
  /** ISO time (device clock) it was queued. */
  queuedAt: string
  /** HTTP-date of the server time this write was based on; sent as `If-Unmodified-Since`. Absent → sent without it. */
  baseTime?: string
  status: OutboxStatus
  /** Server message for a conflict or rejection. */
  error?: string
  /** Records the write creates or changes (`OfflineMutationDef.recordIds`). */
  recordIds?: string[]
  /** Records that must exist on the server first (`OfflineMutationDef.dependsOn`). */
  dependsOn?: string[]
  /** Runs in a row that stopped on a server error (5xx) for this item. */
  serverErrors?: number
  /** The staff user who made the change; only sent while they are signed in. */
  userId?: string
  /** Their display name, for the sync page. */
  userName?: string
}

/** Server errors in a row (separate runs) after which an item is marked rejected. */
export const MAX_SERVER_ERRORS = 5

export interface OutboxUser {
  id: string
  name: string
}

export interface ReplayResult {
  /** Items the server accepted in this run. */
  sent: number
  /** Items newly marked conflict or rejected in this run. */
  failed: number
  /**
   * Why the run ended early: a network failure; a server error (5xx); the
   * server answered 401 (the session ended); nobody is signed in; or another
   * run (this or another tab) is in progress.
   */
  stopped?: 'network' | 'server' | 'unauthorized' | 'signed-out' | 'busy'
}

export interface OutboxOptions {
  store: KeyValueStore<OutboxItem>
  getDefinition: (key: string) => AnyOfflineMutationDef | undefined
  getQueryClient: () => QueryClient
  newId: () => string
  /**
   * The signed-in staff user, stamped on new items and used to choose which
   * items to send. Without this option ownership is not tracked (every item is
   * sent); with it, nothing is sent while it returns undefined.
   */
  getCurrentUser?: () => OutboxUser | undefined
  /**
   * Runs `fn` while holding an exclusive lock shared by all tabs, or resolves
   * `undefined` without running it when another holder has the lock.
   * Defaults to no cross-tab locking.
   */
  withExclusiveLock?: <T>(fn: () => Promise<T>) => Promise<T | undefined>
  /** Tells other tabs the outbox changed. */
  notifyOtherTabs?: () => void
  now?: () => Date
}

export interface Outbox {
  /** Reads the outbox from storage (at start-up and when another tab changed it). */
  load: () => Promise<void>
  /** All items in order, every user's. The array is replaced on every change. */
  getItems: () => readonly OutboxItem[]
  /** Whether an item belongs to the signed-in user (and so may be sent, retried, re-applied). */
  isOwn: (item: OutboxItem) => boolean
  subscribe: (listener: () => void) => () => void
  /** Whether a replay run is in progress in this tab. */
  isReplaying: () => boolean
  /**
   * Whether a new write must be queued rather than sent now to keep order:
   * any of the user's items is still queued, or any item (anyone's) that
   * creates or changes one of the records in `ids` (the new write's
   * `recordIds` and `dependsOn`) is waiting.
   */
  mustQueue: (ids?: string[]) => boolean
  /** The user's queued items that are not blocked behind a conflict/rejected/parked item of the same record. */
  sendableCount: () => number
  /** Queues a write; `baseTime` is the server time of the data it was made from (`If-Unmodified-Since`). */
  enqueue: <TVars, TResult>(
    def: OfflineMutationDef<TVars, TResult>,
    vars: TVars,
    extra?: { baseTime?: string },
  ) => Promise<OutboxItem>
  replay: () => Promise<ReplayResult>
  /** Queue a conflict/rejected item again as it was (still sends `If-Unmodified-Since`). */
  retry: (id: string) => Promise<void>
  /** Queue a conflict item again without `If-Unmodified-Since`, so it replaces the other change. */
  overwrite: (id: string) => Promise<void>
  /** Drop an item: the change is lost. */
  discard: (id: string) => Promise<void>
  /**
   * Drop items (sign-out: the signed-in user's own; no filter: all) and abort
   * a running replay.
   */
  clear: (filter?: (item: OutboxItem) => boolean) => Promise<void>
}

function intersects(ids: readonly string[] | undefined, blocked: ReadonlySet<string>): boolean {
  return !!ids?.some(id => blocked.has(id))
}

/** Whether an item creates/changes or depends on one of the `blocked` records. */
function touchesBlocked(item: Pick<OutboxItem, 'recordIds' | 'dependsOn'>, blocked: ReadonlySet<string>): boolean {
  return intersects(item.recordIds, blocked) || intersects(item.dependsOn, blocked)
}

function sortItems(items: OutboxItem[]): OutboxItem[] {
  return items.sort((a, b) => a.seq - b.seq || a.id.localeCompare(b.id))
}

/**
 * Which queued items may be sent now: walks the outbox in order, collecting
 * the `recordIds` of conflict/rejected items and of other users' (parked)
 * items; a queued item that changes or depends on one of them is blocked, and
 * blocks later items for its own records in turn.
 */
export function partitionSendable(
  items: readonly OutboxItem[],
  isOwn: (item: OutboxItem) => boolean = () => true,
): { sendable: OutboxItem[], blocked: OutboxItem[], parked: OutboxItem[] } {
  const blockedIds = new Set<string>()
  const sendable: OutboxItem[] = []
  const blocked: OutboxItem[] = []
  const parked: OutboxItem[] = []
  for (const item of items) {
    if (!isOwn(item)) {
      parked.push(item)
      for (const id of item.recordIds ?? []) blockedIds.add(id)
      continue
    }
    if (item.status !== 'queued' || touchesBlocked(item, blockedIds)) {
      if (item.status === 'queued') blocked.push(item)
      for (const id of item.recordIds ?? []) blockedIds.add(id)
      continue
    }
    sendable.push(item)
  }
  return { sendable, blocked, parked }
}

export function createOutbox(options: OutboxOptions): Outbox {
  const { store } = options
  const now = options.now ?? (() => new Date())
  const withLock = options.withExclusiveLock ?? (fn => fn())
  let items: readonly OutboxItem[] = []
  const listeners = new Set<() => void>()
  let running: Promise<ReplayResult> | undefined
  let runAgain = false
  let controller: AbortController | undefined
  let lastSeq = 0
  const tracksUsers = !!options.getCurrentUser

  /** Items made before ownership was recorded have no `userId` and count as the current user's. */
  function isOwn(item: OutboxItem): boolean {
    if (!tracksUsers || item.userId === undefined) return true
    return item.userId === options.getCurrentUser?.()?.id
  }

  function emit() {
    for (const listener of listeners) listener()
  }

  function setItems(next: OutboxItem[]) {
    items = sortItems(next)
    lastSeq = Math.max(lastSeq, ...items.map(item => item.seq))
    emit()
  }

  async function changed() {
    options.notifyOtherTabs?.()
  }

  async function load() {
    const stored = await store.entries()
    setItems(stored.map(([, item]) => item))
  }

  async function update(id: string, patch: Partial<OutboxItem>, remove: (keyof OutboxItem)[] = []) {
    // Not in storage: sent, discarded or cleared (sign-out) here or in another tab. Never bring it back.
    const current = await store.get(id)
    if (!current) {
      setItems(items.filter(item => item.id !== id))
      return
    }
    const next: OutboxItem = { ...current, ...patch }
    for (const key of remove) delete next[key]
    await store.set(id, next)
    setItems(items.map(item => (item.id === id ? next : item)))
    await changed()
  }

  async function removeItem(id: string) {
    await store.del(id)
    setItems(items.filter(item => item.id !== id))
    await changed()
  }

  async function enqueue<TVars, TResult>(
    def: OfflineMutationDef<TVars, TResult>,
    vars: TVars,
    extra: { baseTime?: string } = {},
  ): Promise<OutboxItem> {
    // JSON round-trip: what is stored must be re-runnable from plain data, and
    // reactive proxies cannot be stored in IndexedDB.
    const plainVars = JSON.parse(JSON.stringify(vars ?? null)) as TVars
    const queuedAt = now()
    // Monotonic even for several writes in the same millisecond.
    const seq = Math.max(queuedAt.getTime() * 1000, lastSeq + 1)
    lastSeq = seq
    const item: OutboxItem = {
      id: options.newId(),
      seq,
      key: def.key,
      vars: plainVars,
      label: def.describe(plainVars),
      queuedAt: queuedAt.toISOString(),
      status: 'queued',
    }
    if (extra.baseTime) item.baseTime = extra.baseTime
    const recordIds = def.recordIds?.(plainVars)
    if (recordIds?.length) item.recordIds = [...recordIds]
    const dependsOn = def.dependsOn?.(plainVars)
    if (dependsOn?.length) item.dependsOn = [...dependsOn]
    const user = options.getCurrentUser?.()
    if (user) {
      item.userId = user.id
      item.userName = user.name
    }
    await store.set(item.id, item)
    setItems([...items, item])
    await changed()
    return item
  }

  async function runOnce(): Promise<ReplayResult> {
    const result: ReplayResult = { sent: 0, failed: 0 }
    if (tracksUsers && !options.getCurrentUser?.()) return { ...result, stopped: 'signed-out' }
    controller = new AbortController()
    const { signal } = controller
    // Another tab may have changed the outbox since this tab last read it.
    await load()

    const blockedIds = new Set<string>()
    for (const listed of items) {
      if (signal.aborted) break
      // Re-read: another tab may have sent, retried or discarded it meanwhile.
      const item = await store.get(listed.id)
      if (!item) continue
      if (!isOwn(item) || item.status !== 'queued' || touchesBlocked(item, blockedIds)) {
        for (const id of item.recordIds ?? []) blockedIds.add(id)
        continue
      }

      const def = options.getDefinition(item.key)
      if (!def) {
        await update(item.id, { status: 'rejected', error: `This app version cannot send "${item.key}" changes.` })
        for (const id of item.recordIds ?? []) blockedIds.add(id)
        result.failed++
        continue
      }

      const queryClient = options.getQueryClient()
      const headers: Record<string, string> = item.baseTime ? { 'If-Unmodified-Since': item.baseTime } : {}
      let response: unknown
      try {
        response = await def.send(item.vars, { queryClient, headers, signal, replay: true })
      }
      catch (error) {
        const failure = classifyReplayError(error)
        if (failure === 'stop') {
          result.stopped = getApiErrorStatus(error) === 401 ? 'unauthorized' : 'network'
          return result
        }
        if (failure === 'server') {
          const serverErrors = (item.serverErrors ?? 0) + 1
          if (serverErrors < MAX_SERVER_ERRORS) {
            // Probably passing (a restart, a deploy): keep the order and try again later.
            await update(item.id, { serverErrors })
            result.stopped = 'server'
            return result
          }
          await update(item.id, { status: 'rejected', error: describeWriteError(error) }, ['serverErrors'])
          for (const id of item.recordIds ?? []) blockedIds.add(id)
          result.failed++
          continue
        }
        await update(item.id, { status: failure, error: describeWriteError(error) })
        for (const id of item.recordIds ?? []) blockedIds.add(id)
        result.failed++
        continue
      }

      await removeItem(item.id)
      result.sent++
      try {
        await def.onSuccess?.(response, item.vars, queryClient)
      }
      catch (error) {
        // The server has the change; a cache update failing must not resend it.
        console.error(`Offline mutation "${item.key}" onSuccess failed`, error)
      }
    }

    if (result.sent + result.failed > 0 && !signal.aborted) {
      await options.getQueryClient().invalidateQueries()
    }
    return result
  }

  function replay(): Promise<ReplayResult> {
    if (running) {
      // Items queued during this run are picked up by one more run after it.
      runAgain = true
      return running
    }
    running = (async () => {
      let total: ReplayResult = { sent: 0, failed: 0 }
      try {
        do {
          runAgain = false
          const run = await withLock(runOnce)
          if (!run) return { ...total, stopped: 'busy' as const }
          total = { sent: total.sent + run.sent, failed: total.failed + run.failed, stopped: run.stopped }
          if (run.stopped) break
        } while (runAgain)
        return total
      }
      finally {
        running = undefined
        controller = undefined
        emit()
      }
    })()
    emit()
    return running
  }

  function mustQueue(ids?: string[]): boolean {
    if (items.some(item => item.status === 'queued' && isOwn(item))) return true
    if (!ids?.length) return false
    const waiting = new Set(items.flatMap(item => item.recordIds ?? []))
    return ids.some(id => waiting.has(id))
  }

  return {
    load,
    getItems: () => items,
    isOwn,
    subscribe(listener) {
      listeners.add(listener)
      return () => listeners.delete(listener)
    },
    isReplaying: () => !!running,
    mustQueue,
    sendableCount: () => (tracksUsers && !options.getCurrentUser?.() ? 0 : partitionSendable(items, isOwn).sendable.length),
    enqueue,
    replay,
    retry: id => update(id, { status: 'queued' }, ['error', 'serverErrors']),
    overwrite: id => update(id, { status: 'queued' }, ['error', 'baseTime', 'serverErrors']),
    discard: id => removeItem(id),
    async clear(filter) {
      controller?.abort()
      if (!filter) {
        await store.clear()
        setItems([])
      }
      else {
        const stored = (await store.entries()).map(([, item]) => item)
        const dropped = stored.filter(filter)
        for (const item of dropped) await store.del(item.id)
        setItems(stored.filter(item => !filter(item)))
      }
      await changed()
    },
  }
}

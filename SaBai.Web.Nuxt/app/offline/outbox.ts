import type { QueryClient } from '@tanstack/vue-query'
import type { AnyOfflineMutationDef, OfflineMutationDef } from './defineOfflineMutation'
import { classifyReplayError, describeWriteError } from './network'
import type { KeyValueStore } from './storage'

/**
 * The outbox: writes that could not be sent yet, persisted in IndexedDB and
 * replayed strictly in order, one at a time.
 *
 * Replay of one item:
 * - success → the definition's `onSuccess` runs and the item is removed;
 * - network failure / timeout / 401 / 408 / 429 → the run stops, everything
 *   stays queued in order and is tried again later;
 * - 412 → the item is marked `conflict`;
 * - any other error → the item is marked `rejected` with the server's message.
 *
 * A `conflict` or `rejected` item does not hold up the rest of the outbox,
 * except later items for the same record (sharing an id from the definition's
 * `recordIds`): those stay queued behind it until it is retried, overwritten
 * or discarded. After a run that sent anything and was not stopped, all
 * queries are invalidated so the cache matches the server.
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
  recordIds?: string[]
}

export interface ReplayResult {
  /** Items the server accepted in this run. */
  sent: number
  /** Items newly marked conflict or rejected in this run. */
  failed: number
  /** Why the run ended early: a network failure, or another run (this or another tab) is in progress. */
  stopped?: 'network' | 'busy'
}

export interface OutboxOptions {
  store: KeyValueStore<OutboxItem>
  getDefinition: (key: string) => AnyOfflineMutationDef | undefined
  getQueryClient: () => QueryClient
  /** Server time to stamp on newly queued items (see serverTime.ts). */
  getBaseTime: () => string | undefined
  newId: () => string
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
  /** Items in order. The array is replaced on every change. */
  getItems: () => readonly OutboxItem[]
  subscribe: (listener: () => void) => () => void
  /** Whether a replay run is in progress in this tab. */
  isReplaying: () => boolean
  /**
   * Whether a new write must be queued rather than sent now to keep order:
   * any item is still queued, or an item for one of `recordIds` is waiting.
   */
  mustQueue: (recordIds?: string[]) => boolean
  /** Items still queued that are not blocked behind a conflict/rejected item of the same record. */
  sendableCount: () => number
  enqueue: <TVars, TResult>(def: OfflineMutationDef<TVars, TResult>, vars: TVars) => Promise<OutboxItem>
  replay: () => Promise<ReplayResult>
  /** Queue a conflict/rejected item again as it was (still sends `If-Unmodified-Since`). */
  retry: (id: string) => Promise<void>
  /** Queue a conflict item again without `If-Unmodified-Since`, so it replaces the other change. */
  overwrite: (id: string) => Promise<void>
  /** Drop an item: the change is lost. */
  discard: (id: string) => Promise<void>
  /** Drop everything (sign-out) and abort a running replay. */
  clear: () => Promise<void>
}

function intersects(ids: readonly string[] | undefined, blocked: ReadonlySet<string>): boolean {
  return !!ids?.some(id => blocked.has(id))
}

function sortItems(items: OutboxItem[]): OutboxItem[] {
  return items.sort((a, b) => a.seq - b.seq || a.id.localeCompare(b.id))
}

/**
 * Which queued items may be sent now: walks the outbox in order, collecting
 * the record ids of conflict/rejected items; a queued item sharing one of them
 * is blocked, and blocks later items for its own records in turn.
 */
export function partitionSendable(items: readonly OutboxItem[]): { sendable: OutboxItem[], blocked: OutboxItem[] } {
  const blockedIds = new Set<string>()
  const sendable: OutboxItem[] = []
  const blocked: OutboxItem[] = []
  for (const item of items) {
    if (item.status !== 'queued' || intersects(item.recordIds, blockedIds)) {
      if (item.status === 'queued') blocked.push(item)
      for (const id of item.recordIds ?? []) blockedIds.add(id)
      continue
    }
    sendable.push(item)
  }
  return { sendable, blocked }
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
    const current = (await store.get(id)) ?? items.find(item => item.id === id)
    if (!current) return
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

  async function enqueue<TVars, TResult>(def: OfflineMutationDef<TVars, TResult>, vars: TVars): Promise<OutboxItem> {
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
    const baseTime = options.getBaseTime()
    if (baseTime) item.baseTime = baseTime
    const recordIds = def.recordIds?.(plainVars)
    if (recordIds?.length) item.recordIds = [...recordIds]
    await store.set(item.id, item)
    setItems([...items, item])
    await changed()
    return item
  }

  async function runOnce(): Promise<ReplayResult> {
    const result: ReplayResult = { sent: 0, failed: 0 }
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
      if (item.status !== 'queued' || intersects(item.recordIds, blockedIds)) {
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
        response = await def.send(item.vars, { queryClient, headers, signal })
      }
      catch (error) {
        const failure = classifyReplayError(error)
        if (failure === 'stop') {
          result.stopped = 'network'
          return result
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

  function mustQueue(recordIds?: string[]): boolean {
    if (items.some(item => item.status === 'queued')) return true
    if (!recordIds?.length) return false
    const waiting = new Set(items.flatMap(item => item.recordIds ?? []))
    return recordIds.some(id => waiting.has(id))
  }

  return {
    load,
    getItems: () => items,
    subscribe(listener) {
      listeners.add(listener)
      return () => listeners.delete(listener)
    },
    isReplaying: () => !!running,
    mustQueue,
    sendableCount: () => partitionSendable(items).sendable.length,
    enqueue,
    replay,
    retry: id => update(id, { status: 'queued' }, ['error']),
    overwrite: id => update(id, { status: 'queued' }, ['error', 'baseTime']),
    discard: id => removeItem(id),
    async clear() {
      controller?.abort()
      await store.clear()
      setItems([])
      await changed()
    },
  }
}

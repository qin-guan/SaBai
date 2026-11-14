import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createConnectivity } from '~/offline/connectivity'
import { defineOfflineMutation, getOfflineMutation, listOfflineMutationKeys, requestOptions } from '~/offline/defineOfflineMutation'
import { classifyReplayError, isNetworkError, NetworkError } from '~/offline/network'
import { getPrefetchState, maybeRunOfflinePrefetch, registerOfflinePrefetch, resetPrefetchState, runOfflinePrefetch } from '~/offline/prefetch'
import { sanitizePersistedClient, shouldPersistQuery } from '~/offline/queryPersistence'
import { clearServerTime, recordServerDate, serverTimeAt } from '~/offline/serverTime'
import { httpError, newQueryClient } from './helpers'

describe('isNetworkError / classifyReplayError', () => {
  it.each([
    ['NetworkError from the transport', new NetworkError(), true, 'stop'],
    ['timeout', new DOMException('timed out', 'TimeoutError'), true, 'stop'],
    ['fetch TypeError (Chrome)', new TypeError('Failed to fetch'), true, 'stop'],
    ['fetch TypeError (Safari)', new TypeError('Load failed'), true, 'stop'],
    ['proxy 502', httpError(502), true, 'stop'],
    ['gateway 503', httpError(503), true, 'stop'],
    ['gateway 504', httpError(504), true, 'stop'],
    ['caller abort', new DOMException('aborted', 'AbortError'), false, 'stop'],
    ['programming TypeError', new TypeError('x is undefined'), false, 'rejected'],
    ['400', httpError(400), false, 'rejected'],
    ['401', httpError(401), false, 'stop'],
    ['404', httpError(404), false, 'rejected'],
    ['409', httpError(409), false, 'rejected'],
    ['412', httpError(412), false, 'conflict'],
    ['500', httpError(500), false, 'server'],
    ['undefined', undefined, false, 'rejected'],
  ])('%s', (_name, error, network, replay) => {
    expect(isNetworkError(error)).toBe(network)
    expect(classifyReplayError(error)).toBe(replay)
  })
})

describe('defineOfflineMutation registry', () => {
  it('registers by key, and re-registering the same key replaces it (hot reload)', () => {
    const first = defineOfflineMutation({ key: 'test.registry', describe: () => 'one', send: async () => 1 })
    const second = defineOfflineMutation({ key: 'test.registry', describe: () => 'two', send: async () => 2 })
    expect(getOfflineMutation('test.registry')).toBe(second)
    expect(getOfflineMutation('test.registry')).not.toBe(first)
    expect(listOfflineMutationKeys().filter(k => k === 'test.registry')).toHaveLength(1)
  })

  it('requires a key', () => {
    expect(() => defineOfflineMutation({ key: '', describe: () => '', send: async () => 0 })).toThrow()
  })

  it('requestOptions carries headers in options.headers and the signal', () => {
    const signal = new AbortController().signal
    expect(requestOptions({ headers: { 'If-Unmodified-Since': 'x' }, signal })).toEqual({
      signal,
      options: { headers: { 'If-Unmodified-Since': 'x' } },
    })
  })
})

describe('serverTime', () => {
  beforeEach(() => clearServerTime())

  it('converts a device time to server time from the latest Date header, rounded down to the second', () => {
    expect(serverTimeAt(Date.now())).toBeUndefined()
    // The server clock is 1 h ahead of the device's.
    const receivedAt = Date.parse('2026-11-14T08:30:00.000Z')
    recordServerDate('Sat, 14 Nov 2026 09:30:00 GMT', receivedAt)
    expect(serverTimeAt(receivedAt)).toBe('Sat, 14 Nov 2026 09:30:00 GMT')
    // Data fetched ten minutes and a half second earlier (device clock).
    expect(serverTimeAt(receivedAt - 10 * 60_000 - 500)).toBe('Sat, 14 Nov 2026 09:19:59 GMT')
    recordServerDate('Sat, 14 Nov 2026 09:00:00 GMT', receivedAt + 1000) // older: ignored
    recordServerDate('not a date')
    recordServerDate(null)
    expect(serverTimeAt(receivedAt)).toBe('Sat, 14 Nov 2026 09:30:00 GMT')
  })
})

describe('connectivity', () => {
  it('goes offline on a network failure and back online on the next response', () => {
    const onReachable = vi.fn()
    const c = createConnectivity({ probe: async () => true, onReachable })
    const states: boolean[] = []
    c.subscribe(s => states.push(s.offline))
    c.reportNetworkFailure()
    c.reportNetworkFailure()
    c.reportReachable()
    expect(states).toEqual([true, false])
    expect(onReachable).toHaveBeenCalledTimes(1)
  })

  it('probes: a failed probe means offline, a successful one online; concurrent probes share one request', async () => {
    let ok = false
    const probe = vi.fn(async () => ok)
    const onReachable = vi.fn()
    const c = createConnectivity({ probe, onReachable })
    await Promise.all([c.probeNow(), c.probeNow()])
    expect(probe).toHaveBeenCalledTimes(1)
    expect(c.getState().offline).toBe(true)
    ok = true
    await c.probeNow()
    expect(c.getState().offline).toBe(false)
    expect(onReachable).toHaveBeenCalledTimes(1)
    // While online, a successful probe still reports reachability (queued writes get sent).
    await c.probeNow()
    expect(onReachable).toHaveBeenCalledTimes(2)
  })

  it('trusts offline events, verifies online events, and probes on an interval only when needed', async () => {
    vi.useFakeTimers()
    try {
      const target = new EventTarget() as unknown as Window
      const probe = vi.fn(async () => true)
      let queued = false
      const c = createConnectivity({ probe, intervalMs: 1000, shouldProbeWhileOnline: () => queued })
      c.start(target)
      await vi.advanceTimersByTimeAsync(3000)
      expect(probe).not.toHaveBeenCalled()

      target.dispatchEvent(new Event('offline'))
      expect(c.getState().offline).toBe(true)
      target.dispatchEvent(new Event('online'))
      expect(probe).toHaveBeenCalledTimes(1)
      await vi.advanceTimersByTimeAsync(0)
      expect(c.getState().offline).toBe(false)

      queued = true
      await vi.advanceTimersByTimeAsync(1000)
      expect(probe).toHaveBeenCalledTimes(2)
      c.stop()
      await vi.advanceTimersByTimeAsync(5000)
      expect(probe).toHaveBeenCalledTimes(2)
    }
    finally {
      vi.useRealTimers()
    }
  })
})

describe('offline prefetch', () => {
  afterEach(() => resetPrefetchState())

  it('runs every registered prefetch with bounded concurrency and reports progress and failures', async () => {
    let running = 0
    let maxRunning = 0
    const tick = () => new Promise(resolve => setTimeout(resolve, 2))
    for (let i = 0; i < 6; i++) {
      registerOfflinePrefetch(`test-${i}`, async () => {
        running++
        maxRunning = Math.max(maxRunning, running)
        await tick()
        running--
        if (i === 4) throw new Error('nope')
      })
    }
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    const qc = newQueryClient()
    const run = runOfflinePrefetch(qc, { concurrency: 2 })
    expect(runOfflinePrefetch(qc)).toBe(run)
    expect(getPrefetchState().running).toBe(true)
    const final = await run
    warn.mockRestore()
    expect(maxRunning).toBe(2)
    expect(final.done).toBe(final.total)
    expect(final.failed).toEqual(['test-4'])
    expect(final.running).toBe(false)
    // Only complete runs count for the interval.
    expect(final.lastRunAt).toBeUndefined()
  })

  it('re-registering a name replaces it', async () => {
    const first = vi.fn(async () => {})
    const second = vi.fn(async () => {})
    registerOfflinePrefetch('test-0', first)
    registerOfflinePrefetch('test-0', second)
    for (let i = 1; i < 6; i++) registerOfflinePrefetch(`test-${i}`, async () => {})
    await runOfflinePrefetch(newQueryClient())
    expect(first).not.toHaveBeenCalled()
    expect(second).toHaveBeenCalledTimes(1)
  })

  it('skips prefetches limited to roles the user does not have', async () => {
    const clinical = vi.fn(async () => {})
    const everyone = vi.fn(async () => {})
    registerOfflinePrefetch('test-0', clinical, { roles: ['admin', 'member'] })
    registerOfflinePrefetch('test-1', everyone)
    for (let i = 2; i < 6; i++) registerOfflinePrefetch(`test-${i}`, async () => {})
    const qc = newQueryClient()

    await runOfflinePrefetch(qc, { roles: ['pn'] })
    expect(clinical).not.toHaveBeenCalled()
    expect(everyone).toHaveBeenCalledTimes(1)

    await runOfflinePrefetch(qc, { roles: ['member'] })
    expect(clinical).toHaveBeenCalledTimes(1)
    registerOfflinePrefetch('test-0', async () => {})
  })

  it('maybeRun skips a run within the interval', async () => {
    for (let i = 0; i < 6; i++) registerOfflinePrefetch(`test-${i}`, async () => {})
    const qc = newQueryClient()
    const now = Date.now()
    await maybeRunOfflinePrefetch(qc, { now })
    expect(getPrefetchState().lastRunAt).toBeDefined()
    expect(maybeRunOfflinePrefetch(qc, { now: now + 60_000 })).toBeUndefined()
    expect(maybeRunOfflinePrefetch(qc, { now: now + 31 * 60_000 })).toBeDefined()
  })
})

describe('query persistence filters', () => {
  it('persists queries with data, unless meta.persist is false', () => {
    const state = (data: unknown) => ({ data }) as never
    expect(shouldPersistQuery({ state: state([1]), meta: undefined })).toBe(true)
    expect(shouldPersistQuery({ state: state(undefined), meta: undefined })).toBe(false)
    expect(shouldPersistQuery({ state: state([1]), meta: { persist: false } })).toBe(false)
  })

  it('saves an errored query as its last good data and drops mutations', () => {
    const client = sanitizePersistedClient({
      buster: 'b',
      timestamp: 1,
      clientState: {
        mutations: [{ state: {} } as never],
        queries: [{
          queryKey: ['x'],
          queryHash: '["x"]',
          state: {
            data: [1],
            status: 'error',
            error: new Error('offline'),
            fetchStatus: 'fetching',
            fetchFailureCount: 1,
            fetchFailureReason: new Error('offline'),
          },
        } as never],
      },
    })
    expect(client.clientState.mutations).toEqual([])
    expect(client.clientState.queries[0]!.state).toMatchObject({
      data: [1],
      status: 'success',
      error: null,
      fetchStatus: 'idle',
      fetchFailureCount: 0,
      fetchFailureReason: null,
    })
  })
})

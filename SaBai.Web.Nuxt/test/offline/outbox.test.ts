import { describe, expect, it, vi } from 'vitest'
import type { OfflineMutationContext, OfflineMutationDef } from '~/offline/defineOfflineMutation'
import { partitionSendable, type OutboxItem } from '~/offline/outbox'
import { httpError, makeOutbox, networkError } from './helpers'

interface Vars { recordId: string, n: number }

/** A definition whose `send` is a mock: records each call's vars and headers. */
function recordingDef(key = 'test.write') {
  const calls: Array<{ vars: Vars, headers: Record<string, string> }> = []
  const send = vi.fn(async (vars: Vars, ctx: OfflineMutationContext) => {
    calls.push({ vars, headers: ctx.headers })
    return { ok: vars.n }
  })
  const onSuccess = vi.fn()
  const def: OfflineMutationDef<Vars, { ok: number }> = {
    key,
    describe: vars => `Write ${vars.n} to ${vars.recordId}`,
    send,
    onSuccess,
    recordIds: vars => [vars.recordId],
  }
  return { def, send, onSuccess, calls }
}

describe('outbox', () => {
  it('replays items one at a time in the order they were queued, then removes them', async () => {
    const { def, calls, onSuccess } = recordingDef()
    const { outbox, store } = makeOutbox([def])
    for (let n = 1; n <= 5; n++) await outbox.enqueue(def, { recordId: `r${n % 2}`, n })

    let inFlight = 0
    let maxInFlight = 0
    def.send = async (vars, ctx) => {
      inFlight++
      maxInFlight = Math.max(maxInFlight, inFlight)
      await new Promise(resolve => setTimeout(resolve, 1))
      inFlight--
      calls.push({ vars, headers: ctx.headers })
      return { ok: vars.n }
    }

    const result = await outbox.replay()
    expect(result).toEqual({ sent: 5, failed: 0, stopped: undefined })
    expect(calls.map(c => c.vars.n)).toEqual([1, 2, 3, 4, 5])
    expect(maxInFlight).toBe(1)
    expect(onSuccess).toHaveBeenCalledTimes(5)
    expect(outbox.getItems()).toEqual([])
    expect(await store.entries()).toEqual([])
  })

  it('keeps the order of writes queued in the same millisecond', async () => {
    const { def, calls } = recordingDef()
    const fixed = new Date('2026-11-14T10:00:00Z')
    const { outbox } = makeOutbox([def], { now: () => fixed, newId: (() => { let i = 100; return () => `z-${i--}` })() })
    for (let n = 1; n <= 4; n++) await outbox.enqueue(def, { recordId: 'r', n })
    await outbox.replay()
    expect(calls.map(c => c.vars.n)).toEqual([1, 2, 3, 4])
  })

  it('stamps the base time when queued and sends it as If-Unmodified-Since on replay', async () => {
    const { def, calls } = recordingDef()
    let serverTime: string | undefined = 'Fri, 13 Nov 2026 08:00:00 GMT'
    const { outbox } = makeOutbox([def], { getBaseTime: () => serverTime })
    const first = await outbox.enqueue(def, { recordId: 'a', n: 1 })
    serverTime = 'Sat, 14 Nov 2026 09:30:00 GMT'
    const second = await outbox.enqueue(def, { recordId: 'b', n: 2 })
    serverTime = undefined
    const third = await outbox.enqueue(def, { recordId: 'c', n: 3 })

    expect(first.baseTime).toBe('Fri, 13 Nov 2026 08:00:00 GMT')
    expect(second.baseTime).toBe('Sat, 14 Nov 2026 09:30:00 GMT')
    expect(third.baseTime).toBeUndefined()

    await outbox.replay()
    expect(calls.map(c => c.headers)).toEqual([
      { 'If-Unmodified-Since': 'Fri, 13 Nov 2026 08:00:00 GMT' },
      { 'If-Unmodified-Since': 'Sat, 14 Nov 2026 09:30:00 GMT' },
      {},
    ])
  })

  it('stores vars as plain JSON data and labels items with describe()', async () => {
    const { def } = recordingDef()
    const { outbox, store } = makeOutbox([def])
    const vars = { recordId: 'a', n: 7, extra: undefined, when: new Date('2026-11-14T00:00:00Z') }
    const item = await outbox.enqueue(def, vars as unknown as Vars)
    const stored = await store.get(item.id)
    expect(stored?.vars).toEqual({ recordId: 'a', n: 7, when: '2026-11-14T00:00:00.000Z' })
    expect(stored?.label).toBe('Write 7 to a')
    expect(stored?.recordIds).toEqual(['a'])
    expect(stored?.status).toBe('queued')
  })

  it('stops at a network failure and keeps everything queued in order', async () => {
    const { def, calls } = recordingDef()
    const { outbox } = makeOutbox([def])
    for (let n = 1; n <= 3; n++) await outbox.enqueue(def, { recordId: `r${n}`, n })
    def.send = vi.fn(async (vars: Vars) => {
      if (vars.n === 2) throw networkError()
      calls.push({ vars, headers: {} })
      return { ok: vars.n }
    })

    const result = await outbox.replay()
    expect(result.stopped).toBe('network')
    expect(result.sent).toBe(1)
    expect(outbox.getItems().map(i => [(i.vars as Vars).n, i.status])).toEqual([[2, 'queued'], [3, 'queued']])
    // Item 3 was not attempted: order is kept.
    expect(def.send).toHaveBeenCalledTimes(2)
  })

  it.each([
    [401, 'unauthorized'],
    [408, 'network'],
    [429, 'network'],
    [502, 'network'],
    [503, 'network'],
    [504, 'network'],
  ])('treats HTTP %i as "try again later"', async (status, stopped) => {
    const { def } = recordingDef()
    const { outbox } = makeOutbox([def])
    await outbox.enqueue(def, { recordId: 'a', n: 1 })
    def.send = async () => { throw httpError(status) }
    const result = await outbox.replay()
    expect(result.stopped).toBe(stopped)
    expect(outbox.getItems()[0]?.status).toBe('queued')
  })

  it('marks a 412 as conflict and other 4xx/5xx as rejected, with the server message', async () => {
    const { def } = recordingDef()
    const { outbox } = makeOutbox([def])
    await outbox.enqueue(def, { recordId: 'a', n: 1 })
    await outbox.enqueue(def, { recordId: 'b', n: 2 })
    await outbox.enqueue(def, { recordId: 'c', n: 3 })
    await outbox.enqueue(def, { recordId: 'd', n: 4 })
    def.send = async (vars: Vars) => {
      if (vars.n === 1) throw httpError(412, 'Changed by someone else.')
      if (vars.n === 2) throw httpError(400, 'Code is required.')
      if (vars.n === 3) throw httpError(500)
      return { ok: vars.n }
    }

    const result = await outbox.replay()
    expect(result).toEqual({ sent: 1, failed: 3, stopped: undefined })
    expect(outbox.getItems().map(i => [i.status, i.error])).toEqual([
      ['conflict', 'Changed by someone else.'],
      ['rejected', 'Code is required.'],
      ['rejected', 'Request failed with status 500'],
    ])
  })

  it('holds later writes for the same record behind a conflict, and sends the rest', async () => {
    const { def, calls } = recordingDef()
    const { outbox } = makeOutbox([def])
    await outbox.enqueue(def, { recordId: 'patient-1', n: 1 })
    await outbox.enqueue(def, { recordId: 'patient-2', n: 2 })
    await outbox.enqueue(def, { recordId: 'patient-1', n: 3 })
    await outbox.enqueue(def, { recordId: 'patient-3', n: 4 })
    const send = def.send
    def.send = async (vars, ctx) => {
      if (vars.n === 1) throw httpError(412)
      return send(vars, ctx)
    }

    await outbox.replay()
    expect(calls.map(c => c.vars.n)).toEqual([2, 4])
    expect(outbox.getItems().map(i => [(i.vars as Vars).n, i.status])).toEqual([[1, 'conflict'], [3, 'queued']])
    expect(partitionSendable(outbox.getItems()).blocked.map(i => (i.vars as Vars).n)).toEqual([3])
    expect(outbox.sendableCount()).toBe(0)

    // Another replay does not send the blocked item either.
    await outbox.replay()
    expect(calls.map(c => c.vars.n)).toEqual([2, 4])

    // Discarding the conflict releases it.
    await outbox.discard(outbox.getItems()[0]!.id)
    await outbox.replay()
    expect(calls.map(c => c.vars.n)).toEqual([2, 4, 3])
    expect(outbox.getItems()).toEqual([])
  })

  it('blocks transitively: a blocked write also holds back later writes for its other records', () => {
    const item = (n: number, status: OutboxItem['status'], recordIds: string[]): OutboxItem =>
      ({ id: `i${n}`, seq: n, key: 'k', vars: { n }, label: '', queuedAt: '', status, recordIds })
    const { sendable, blocked } = partitionSendable([
      item(1, 'rejected', ['patient']),
      item(2, 'queued', ['patient', 'visit']),
      item(3, 'queued', ['visit']),
      item(4, 'queued', ['other']),
      item(5, 'queued', []),
    ])
    expect(blocked.map(i => i.id)).toEqual(['i2', 'i3'])
    expect(sendable.map(i => i.id)).toEqual(['i4', 'i5'])
  })

  it('overwrite resends without If-Unmodified-Since; retry keeps it', async () => {
    const { def, calls } = recordingDef()
    const { outbox } = makeOutbox([def])
    await outbox.enqueue(def, { recordId: 'a', n: 1 })
    await outbox.enqueue(def, { recordId: 'b', n: 2 })
    const send = def.send
    let fail = true
    def.send = async (vars, ctx) => {
      if (fail) throw httpError(412)
      return send(vars, ctx)
    }
    await outbox.replay()
    expect(outbox.getItems().map(i => i.status)).toEqual(['conflict', 'conflict'])

    fail = false
    const [first, second] = outbox.getItems()
    await outbox.retry(first!.id)
    await outbox.overwrite(second!.id)
    expect(outbox.getItems().map(i => [i.status, i.error, i.baseTime])).toEqual([
      ['queued', undefined, 'Fri, 13 Nov 2026 08:00:00 GMT'],
      ['queued', undefined, undefined],
    ])
    await outbox.replay()
    expect(calls.map(c => c.headers)).toEqual([
      { 'If-Unmodified-Since': 'Fri, 13 Nov 2026 08:00:00 GMT' },
      {},
    ])
  })

  it('rejects items whose definition is not registered', async () => {
    const { def } = recordingDef('gone.key')
    const { outbox } = makeOutbox([])
    await outbox.enqueue(def, { recordId: 'a', n: 1 })
    await outbox.replay()
    expect(outbox.getItems()[0]).toMatchObject({ status: 'rejected' })
  })

  it('runs one replay at a time and picks up writes queued during a run', async () => {
    const { def, calls } = recordingDef()
    const { outbox } = makeOutbox([def])
    await outbox.enqueue(def, { recordId: 'a', n: 1 })
    let release!: () => void
    const gate = new Promise<void>((resolve) => { release = resolve })
    const send = def.send
    def.send = async (vars, ctx) => {
      if (vars.n === 1) await gate
      return send(vars, ctx)
    }

    const first = outbox.replay()
    expect(outbox.isReplaying()).toBe(true)
    await new Promise(resolve => setTimeout(resolve, 0))
    await outbox.enqueue(def, { recordId: 'b', n: 2 })
    const second = outbox.replay()
    expect(second).toBe(first)
    release()
    await first
    expect(calls.map(c => c.vars.n)).toEqual([1, 2])
    expect(outbox.isReplaying()).toBe(false)
  })

  it('does not run when another tab holds the replay lock', async () => {
    const { def } = recordingDef()
    const { outbox } = makeOutbox([def], { withExclusiveLock: async () => undefined })
    await outbox.enqueue(def, { recordId: 'a', n: 1 })
    const result = await outbox.replay()
    expect(result.stopped).toBe('busy')
    expect(def.send).not.toHaveBeenCalled()
  })

  it('re-reads storage, so an item another tab already sent is not sent again', async () => {
    const { def } = recordingDef()
    const { outbox, store } = makeOutbox([def])
    const item = await outbox.enqueue(def, { recordId: 'a', n: 1 })
    await store.del(item.id)
    await outbox.replay()
    expect(def.send).not.toHaveBeenCalled()
    expect(outbox.getItems()).toEqual([])
  })

  it('invalidates all queries after a run that sent something, and not after one that stopped', async () => {
    const { def } = recordingDef()
    const { outbox, queryClient } = makeOutbox([def])
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')
    await outbox.enqueue(def, { recordId: 'a', n: 1 })
    await outbox.replay()
    expect(invalidate).toHaveBeenCalledWith()

    invalidate.mockClear()
    await outbox.enqueue(def, { recordId: 'a', n: 2 })
    def.send = async () => { throw networkError() }
    await outbox.replay()
    expect(invalidate).not.toHaveBeenCalled()
  })

  it('mustQueue: when anything is queued, or a waiting item touches the same record', async () => {
    const { def } = recordingDef()
    const { outbox } = makeOutbox([def])
    expect(outbox.mustQueue(['a'])).toBe(false)
    await outbox.enqueue(def, { recordId: 'a', n: 1 })
    expect(outbox.mustQueue(['z'])).toBe(true)
    def.send = async () => { throw httpError(400) }
    await outbox.replay()
    expect(outbox.mustQueue(['z'])).toBe(false)
    expect(outbox.mustQueue(['a'])).toBe(true)
  })

  it('loads persisted items in order and clears everything', async () => {
    const { def } = recordingDef()
    const { outbox, store } = makeOutbox([def])
    await outbox.enqueue(def, { recordId: 'a', n: 1 })
    await outbox.enqueue(def, { recordId: 'b', n: 2 })
    const { outbox: reloaded } = makeOutbox([def], { store })
    await reloaded.load()
    expect(reloaded.getItems().map(i => (i.vars as Vars).n)).toEqual([1, 2])
    await reloaded.clear()
    expect(reloaded.getItems()).toEqual([])
    expect(await store.entries()).toEqual([])
  })

  it('notifies subscribers and other tabs on every change', async () => {
    const { def } = recordingDef()
    const notifyOtherTabs = vi.fn()
    const { outbox } = makeOutbox([def], { notifyOtherTabs })
    const listener = vi.fn()
    outbox.subscribe(listener)
    await outbox.enqueue(def, { recordId: 'a', n: 1 })
    expect(listener).toHaveBeenCalled()
    expect(notifyOtherTabs).toHaveBeenCalledTimes(1)
  })
})

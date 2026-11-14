import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { claimQueryCache, releaseQueryCache } from '~/offline/cacheOwner'
import type { OfflineMutationContext, OfflineMutationDef } from '~/offline/defineOfflineMutation'
import { runOfflineMutation } from '~/offline/mutate'
import { undoQueuedEdit } from '~/offline/optimistic'
import type { OutboxUser } from '~/offline/outbox'
import { httpError, makeOutbox, newQueryClient } from './helpers'

interface Vars { recordId: string, n: number }

function recordingDef() {
  const sent: number[] = []
  const def: OfflineMutationDef<Vars, number> = {
    key: 'test.owned',
    describe: vars => `Write ${vars.n}`,
    recordIds: vars => [vars.recordId],
    send: vi.fn(async (vars: Vars, _ctx: OfflineMutationContext) => {
      sent.push(vars.n)
      return vars.n
    }),
  }
  return { def, sent }
}

const alice: OutboxUser = { id: 'alice', name: 'Alice' }
const bob: OutboxUser = { id: 'bob', name: 'Bob' }

describe('outbox ownership', () => {
  function setup() {
    let user: OutboxUser | undefined = alice
    const { def, sent } = recordingDef()
    const ctx = makeOutbox([def], { getCurrentUser: () => user })
    return { ...ctx, def, sent, signIn: (next: OutboxUser | undefined) => { user = next } }
  }

  it('stamps the signed-in user on each queued item', async () => {
    const { outbox, def } = setup()
    const item = await outbox.enqueue(def, { recordId: 'a', n: 1 })
    expect(item).toMatchObject({ userId: 'alice', userName: 'Alice' })
  })

  it('sends only the signed-in user\'s items; another user\'s stay parked until they return', async () => {
    const { outbox, def, sent, signIn } = setup()
    await outbox.enqueue(def, { recordId: 'a', n: 1 })
    signIn(bob)
    await outbox.enqueue(def, { recordId: 'b', n: 2 })

    await outbox.replay()
    expect(sent).toEqual([2])
    expect(outbox.getItems().map(i => [(i.vars as Vars).n, i.userId, i.status])).toEqual([[1, 'alice', 'queued']])
    expect(outbox.isOwn(outbox.getItems()[0]!)).toBe(false)
    expect(outbox.sendableCount()).toBe(0)

    signIn(alice)
    expect(outbox.sendableCount()).toBe(1)
    await outbox.replay()
    expect(sent).toEqual([2, 1])
    expect(outbox.getItems()).toEqual([])
  })

  it('a parked item holds back the current user\'s items for the same record only', async () => {
    const { outbox, def, sent, signIn } = setup()
    await outbox.enqueue(def, { recordId: 'patient-1', n: 1 })
    signIn(bob)
    await outbox.enqueue(def, { recordId: 'patient-1', n: 2 })
    await outbox.enqueue(def, { recordId: 'patient-2', n: 3 })
    await outbox.replay()
    expect(sent).toEqual([3])
    expect(outbox.getItems().map(i => (i.vars as Vars).n)).toEqual([1, 2])
  })

  it('new writes queue behind the user\'s own queued items, not behind another user\'s (unless same record)', async () => {
    const { outbox, def, signIn } = setup()
    await outbox.enqueue(def, { recordId: 'patient-1', n: 1 })
    signIn(bob)
    expect(outbox.mustQueue(['patient-9'])).toBe(false)
    expect(outbox.mustQueue(['patient-1'])).toBe(true)
  })

  it('sends nothing while nobody is signed in', async () => {
    const { outbox, def, sent, signIn } = setup()
    await outbox.enqueue(def, { recordId: 'a', n: 1 })
    signIn(undefined)
    expect(outbox.sendableCount()).toBe(0)
    const result = await outbox.replay()
    expect(result.stopped).toBe('signed-out')
    expect(sent).toEqual([])
  })

  it('stops with "unauthorized" on a 401 and keeps the item queued for its owner', async () => {
    const { outbox, def } = setup()
    await outbox.enqueue(def, { recordId: 'a', n: 1 })
    def.send = async () => { throw httpError(401) }
    const result = await outbox.replay()
    expect(result.stopped).toBe('unauthorized')
    expect(outbox.getItems()[0]).toMatchObject({ status: 'queued', userId: 'alice' })
  })

  it('sign-out clears only the signed-in user\'s items', async () => {
    const { outbox, def, signIn, store } = setup()
    await outbox.enqueue(def, { recordId: 'a', n: 1 })
    signIn(bob)
    await outbox.enqueue(def, { recordId: 'b', n: 2 })
    await outbox.clear(outbox.isOwn)
    expect(outbox.getItems().map(i => i.userId)).toEqual(['alice'])
    expect((await store.entries()).map(([, i]) => i.userId)).toEqual(['alice'])
  })

  it('items queued before ownership was recorded count as the current user\'s', async () => {
    const { outbox, store, sent } = setup()
    await store.set('legacy', { id: 'legacy', seq: 1, key: 'test.owned', vars: { recordId: 'x', n: 9 }, label: '', queuedAt: '', status: 'queued' })
    await outbox.replay()
    expect(sent).toEqual([9])
  })
})

describe('cache owner', () => {
  beforeEach(() => {
    const data = new Map<string, string>()
    vi.stubGlobal('localStorage', {
      getItem: (k: string) => data.get(k) ?? null,
      setItem: (k: string, v: string) => void data.set(k, v),
      removeItem: (k: string) => void data.delete(k),
    })
  })
  afterEach(() => vi.unstubAllGlobals())

  it('keeps the cache for the same user and drops it (except the session) for another', () => {
    const qc = newQueryClient()
    const isSession = (key: readonly unknown[]) => key[0] === 'auth'
    qc.setQueryData(['patients', 'list'], ['alice data'])
    qc.setQueryData(['auth', 'me'], { id: 'bob' })

    expect(claimQueryCache(qc, 'alice', isSession)).toBe(false) // first owner
    expect(claimQueryCache(qc, 'alice', isSession)).toBe(false)
    expect(qc.getQueryData(['patients', 'list'])).toEqual(['alice data'])

    expect(claimQueryCache(qc, 'bob', isSession)).toBe(true)
    expect(qc.getQueryData(['patients', 'list'])).toBeUndefined()
    expect(qc.getQueryData(['auth', 'me'])).toEqual({ id: 'bob' })

    releaseQueryCache()
    qc.setQueryData(['patients', 'list'], ['fresh'])
    expect(claimQueryCache(qc, 'alice', isSession)).toBe(false)
    expect(qc.getQueryData(['patients', 'list'])).toEqual(['fresh'])
  })
})

describe('discarding a queued write offline', () => {
  interface Item { id: string, name: string }
  const listKey = ['items', 'list']
  const detailKey = (id: string) => ['items', 'detail', id]
  const def: OfflineMutationDef<Item, Item> = {
    key: 'items.save',
    describe: vars => vars.name,
    send: async vars => vars,
    optimistic: (vars, qc) => {
      qc.setQueryData<Item[]>(listKey, old => old && [vars, ...old.filter(i => i.id !== vars.id)])
      qc.setQueryData<Item>(detailKey(vars.id), vars)
    },
  }

  it('takes the discarded edit off the screen and keeps the other queued edits', async () => {
    const { outbox, queryClient } = makeOutbox([def])
    const runtime = { queryClient, outbox, isOffline: () => true }
    queryClient.setQueryData<Item[]>(listKey, [{ id: 'old', name: 'Server' }])

    await runOfflineMutation(def, { id: 'a', name: 'A' }, runtime)
    await runOfflineMutation(def, { id: 'b', name: 'B' }, runtime)
    expect(queryClient.getQueryData(listKey)).toEqual([{ id: 'b', name: 'B' }, { id: 'a', name: 'A' }, { id: 'old', name: 'Server' }])

    const [first] = outbox.getItems()
    expect(first!.undo).toEqual([
      { queryKey: listKey, existed: true, data: [{ id: 'old', name: 'Server' }] },
      { queryKey: detailKey('a'), existed: false },
    ])
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')
    await outbox.discard(first!.id)
    undoQueuedEdit(queryClient, first!, outbox.getItems(), key => (key === def.key ? def : undefined))

    expect(queryClient.getQueryData(listKey)).toEqual([{ id: 'b', name: 'B' }, { id: 'old', name: 'Server' }])
    expect(queryClient.getQueryCache().find({ queryKey: detailKey('a'), exact: true })).toBeUndefined()
    expect(queryClient.getQueryData(detailKey('b'))).toEqual({ id: 'b', name: 'B' })
    expect(invalidate).toHaveBeenCalledWith({ queryKey: listKey, exact: true })
  })

  it('without an undo record, refetches everything instead', () => {
    const qc = newQueryClient()
    const invalidate = vi.spyOn(qc, 'invalidateQueries')
    undoQueuedEdit(qc, {}, [], () => undefined)
    expect(invalidate).toHaveBeenCalledWith()
  })
})

import type { QueryClient } from '@tanstack/vue-query'
import { describe, expect, it, vi } from 'vitest'
import type { OfflineMutationDef } from '~/offline/defineOfflineMutation'
import { runOfflineMutation, type OfflineMutationRuntime } from '~/offline/mutate'
import { applyOptimistic, keepQueuedEditsVisible } from '~/offline/optimistic'
import { httpError, makeOutbox, networkError } from './helpers'

interface Item { id: string, name: string }
interface Vars { id: string, name: string }

const listKey = ['items', 'list'] as const
const detailKey = (id: string) => ['items', 'detail', id] as const

function upsertDef(send: (vars: Vars) => Promise<Item>): OfflineMutationDef<Vars, Item> {
  return {
    key: 'items.upsert',
    describe: vars => `Save ${vars.name}`,
    recordIds: vars => [vars.id],
    send: vars => send(vars),
    optimistic: (vars, qc) => {
      qc.setQueryData<Item[]>(listKey, old => old && [vars, ...old.filter(i => i.id !== vars.id)])
      qc.setQueryData<Item>(detailKey(vars.id), vars)
    },
    onSuccess: vi.fn(),
  }
}

function setup(def: OfflineMutationDef<Vars, Item>, { offline = false } = {}) {
  const { outbox, queryClient } = makeOutbox([def])
  const scheduleReplay = vi.fn()
  const runtime: OfflineMutationRuntime = { queryClient, outbox, isOffline: () => offline, scheduleReplay }
  queryClient.setQueryData<Item[]>(listKey, [{ id: 'old', name: 'Existing' }])
  return { outbox, queryClient, runtime, scheduleReplay }
}

describe('runOfflineMutation', () => {
  it('sends live when online with an empty outbox, without If-Unmodified-Since', async () => {
    const send = vi.fn(async (vars: Vars) => ({ ...vars, name: `${vars.name} (saved)` }))
    const def = upsertDef(send)
    const spy = vi.spyOn(def, 'send')
    const { runtime, outbox, queryClient } = setup(def)

    const outcome = await runOfflineMutation(def, { id: 'n1', name: 'New' }, runtime)
    expect(outcome).toEqual({ queued: false, result: { id: 'n1', name: 'New (saved)' } })
    expect(spy.mock.calls[0]?.[1].headers).toEqual({})
    expect(def.onSuccess).toHaveBeenCalledWith({ id: 'n1', name: 'New (saved)' }, { id: 'n1', name: 'New' }, queryClient)
    expect(outbox.getItems()).toEqual([])
    expect(queryClient.getQueryData(listKey)).toEqual([{ id: 'n1', name: 'New' }, { id: 'old', name: 'Existing' }])
  })

  it('queues when offline, keeping the optimistic edit, and does not call send', async () => {
    const send = vi.fn(async (vars: Vars) => vars)
    const def = upsertDef(send)
    const { runtime, outbox, queryClient, scheduleReplay } = setup(def, { offline: true })

    const outcome = await runOfflineMutation(def, { id: 'n1', name: 'New' }, runtime)
    expect(outcome).toEqual({ queued: true })
    expect(send).not.toHaveBeenCalled()
    expect(outbox.getItems()).toHaveLength(1)
    expect(scheduleReplay).not.toHaveBeenCalled()
    expect(queryClient.getQueryData(detailKey('n1'))).toEqual({ id: 'n1', name: 'New' })
  })

  it('queues behind earlier queued writes even when online, and asks for a replay', async () => {
    const send = vi.fn(async (vars: Vars) => vars)
    const def = upsertDef(send)
    const { runtime, outbox, scheduleReplay } = setup(def)
    await outbox.enqueue(def, { id: 'first', name: 'First' })

    const outcome = await runOfflineMutation(def, { id: 'n1', name: 'New' }, runtime)
    expect(outcome).toEqual({ queued: true })
    expect(send).not.toHaveBeenCalled()
    expect(outbox.getItems().map(i => (i.vars as Vars).id)).toEqual(['first', 'n1'])
    expect(scheduleReplay).toHaveBeenCalled()
  })

  it('queues a live send that fails for network reasons', async () => {
    const def = upsertDef(async () => { throw networkError() })
    const reportNetworkFailure = vi.fn()
    const { runtime, outbox, queryClient } = setup(def)
    runtime.reportNetworkFailure = reportNetworkFailure

    const outcome = await runOfflineMutation(def, { id: 'n1', name: 'New' }, runtime)
    expect(outcome).toEqual({ queued: true })
    expect(reportNetworkFailure).toHaveBeenCalled()
    expect(outbox.getItems()).toHaveLength(1)
    expect(queryClient.getQueryData(detailKey('n1'))).toEqual({ id: 'n1', name: 'New' })
  })

  it.each([400, 403, 409, 412, 500])('rolls back the optimistic edit and throws on HTTP %i', async (status) => {
    const error = httpError(status, 'No.')
    const def = upsertDef(async () => { throw error })
    const { runtime, outbox, queryClient } = setup(def)
    const listBefore = queryClient.getQueryData(listKey)

    await expect(runOfflineMutation(def, { id: 'n1', name: 'New' }, runtime)).rejects.toBe(error)
    expect(queryClient.getQueryData(listKey)).toBe(listBefore)
    // The detail query did not exist before the edit: it is removed again.
    expect(queryClient.getQueryCache().find({ queryKey: detailKey('n1') })).toBeUndefined()
    expect(outbox.getItems()).toEqual([])
    expect(def.onSuccess).not.toHaveBeenCalled()
  })

  it('a failing onSuccess after the server accepted the write does not fail the save', async () => {
    const def = upsertDef(async vars => vars)
    def.onSuccess = () => { throw new Error('cache bug') }
    const { runtime } = setup(def)
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {})
    await expect(runOfflineMutation(def, { id: 'n1', name: 'New' }, runtime)).resolves.toMatchObject({ queued: false })
    consoleError.mockRestore()
  })
})

describe('applyOptimistic', () => {
  function client(): QueryClient {
    const { queryClient } = makeOutbox([])
    return queryClient
  }

  it('restores exactly the entries the edit touched, and invalidates them', async () => {
    const qc = client()
    qc.setQueryData(['a'], { v: 1 })
    qc.setQueryData(['b'], { v: 1 })
    const untouched = qc.getQueryCache().find({ queryKey: ['b'] })!.state
    const invalidate = vi.spyOn(qc, 'invalidateQueries')

    const rollback = applyOptimistic(qc, () => {
      qc.setQueryData(['a'], { v: 2 })
      qc.setQueryData(['c'], { v: 2 })
    })
    expect(qc.getQueryData(['a'])).toEqual({ v: 2 })
    rollback()
    expect(qc.getQueryData(['a'])).toEqual({ v: 1 })
    expect(qc.getQueryCache().find({ queryKey: ['c'] })).toBeUndefined()
    expect(qc.getQueryCache().find({ queryKey: ['b'] })!.state).toBe(untouched)
    expect(invalidate).toHaveBeenCalledTimes(1)
    const predicate = invalidate.mock.calls[0]![0]!.predicate!
    const queries = qc.getQueryCache().getAll()
    expect(queries.filter(predicate).map(q => q.queryKey)).toEqual([['a']])
  })

  it('undoes a partial edit when the edit throws', () => {
    const qc = client()
    qc.setQueryData(['a'], 1)
    expect(() => applyOptimistic(qc, () => {
      qc.setQueryData(['a'], 2)
      throw new Error('bug')
    })).toThrow('bug')
    expect(qc.getQueryData(['a'])).toBe(1)
  })

  it('is safe when the edited queries are not cached', () => {
    const qc = client()
    const rollback = applyOptimistic(qc, () => {
      qc.setQueryData<number[]>(['missing'], old => old && [...old, 1])
    })
    expect(qc.getQueryCache().getAll()).toHaveLength(0)
    rollback()
  })
})

describe('keepQueuedEditsVisible', () => {
  it('re-applies queued edits after the server data for a query arrives', async () => {
    const def = upsertDef(async vars => vars)
    const { outbox, queryClient } = setup(def, { offline: true })
    await runOfflineMutation(def, { id: 'n1', name: 'Queued' }, { queryClient, outbox, isOffline: () => true })
    keepQueuedEditsVisible(queryClient, outbox.getItems, key => (key === def.key ? def : undefined))

    // A refetch brings the server's list, which does not have the queued item yet.
    await queryClient.fetchQuery({ queryKey: listKey, queryFn: async () => [{ id: 'old', name: 'Server' }] })
    await Promise.resolve()
    expect(queryClient.getQueryData(listKey)).toEqual([{ id: 'n1', name: 'Queued' }, { id: 'old', name: 'Server' }])
  })
})

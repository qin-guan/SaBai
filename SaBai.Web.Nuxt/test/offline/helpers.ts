import { QueryClient } from '@tanstack/vue-query'
import type { AnyOfflineMutationDef } from '~/offline/defineOfflineMutation'
import { createOutbox, type OutboxItem, type OutboxOptions } from '~/offline/outbox'
import { memoryStore } from '~/offline/storage'

/** An SDK-style HTTP error (the SDK's ResponseError carries `status` and `data`). */
export function httpError(status: number, detail?: string) {
  return Object.assign(new Error(`Request failed with status ${status}`), {
    name: 'ResponseError',
    status,
    data: detail ? { detail } : undefined,
  })
}

export function networkError() {
  return Object.assign(new Error('The server could not be reached.'), { name: 'NetworkError' })
}

export function newQueryClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity } } })
}

let counter = 0
export function sequentialId() {
  counter++
  return `item-${String(counter).padStart(6, '0')}`
}

export function makeOutbox(defs: AnyOfflineMutationDef[], overrides: Partial<OutboxOptions> = {}) {
  const store = memoryStore<OutboxItem>()
  const queryClient = newQueryClient()
  const byKey = new Map(defs.map(def => [def.key, def]))
  const outbox = createOutbox({
    store,
    getDefinition: key => byKey.get(key),
    getQueryClient: () => queryClient,
    getBaseTime: () => 'Fri, 13 Nov 2026 08:00:00 GMT',
    newId: sequentialId,
    ...overrides,
  })
  return { outbox, store, queryClient }
}

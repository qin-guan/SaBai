import { describe, expect, it } from 'vitest'
import '~/offline/mutations'
import { listOfflineMutationKeys } from '~/offline/defineOfflineMutation'

// Every domain module, loaded only after the keys registered by the start-up import were read.
const modules = import.meta.glob('../../app/offline/mutations/*.ts')

describe('offline mutation registry', () => {
  it('the start-up import registers every definition, so a write queued before a reload can be replayed', async () => {
    const registeredAtStartup = new Set(listOfflineMutationKeys())
    const defs: { key: string }[] = []
    for (const load of Object.values(modules)) {
      const exports = (await load()) as Record<string, unknown>
      for (const value of Object.values(exports)) {
        if (value && typeof value === 'object' && 'key' in value && 'send' in value && typeof value.send === 'function')
          defs.push(value as { key: string })
      }
    }
    expect(defs.length).toBeGreaterThan(20)
    expect(defs.map(d => d.key).filter(key => !registeredAtStartup.has(key))).toEqual([])
    expect(new Set(defs.map(d => d.key)).size).toBe(defs.length)
  })
})

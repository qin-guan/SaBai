import { describe, expect, it } from 'vitest'
import { uuidv7 } from '~/utils/uuid'

describe('uuidv7', () => {
  it('generates a valid RFC 9562 UUIDv7 string', () => {
    const id = uuidv7()
    expect(id).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/)
  })

  it('generates unique IDs across rapid calls', () => {
    const ids = new Set(Array.from({ length: 200 }, () => uuidv7()))
    expect(ids.size).toBe(200)
  })
})

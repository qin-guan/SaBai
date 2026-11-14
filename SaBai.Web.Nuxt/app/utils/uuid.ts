/**
 * Generates an RFC 9562 UUIDv7 string (time-ordered, lowercase hyphenated)
 * matching .NET's `Guid.CreateVersion7()`.
 */
export function uuidv7(): string {
  const bytes = new Uint8Array(16)
  crypto.getRandomValues(bytes)

  const now = BigInt(Date.now())
  bytes[0] = Number((now >> 40n) & 0xffn)
  bytes[1] = Number((now >> 32n) & 0xffn)
  bytes[2] = Number((now >> 24n) & 0xffn)
  bytes[3] = Number((now >> 16n) & 0xffn)
  bytes[4] = Number((now >> 8n) & 0xffn)
  bytes[5] = Number(now & 0xffn)

  // Version 7 (0111)
  bytes[6] = ((bytes[6] ?? 0) & 0x0f) | 0x70
  // Variant 10xx
  bytes[8] = ((bytes[8] ?? 0) & 0x3f) | 0x80

  const hex = Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('')
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`
}

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

/** Whether `value` is a UUID (any version), as record ids are. */
export function isUuid(value: unknown): value is string {
  return typeof value === 'string' && UUID_PATTERN.test(value)
}

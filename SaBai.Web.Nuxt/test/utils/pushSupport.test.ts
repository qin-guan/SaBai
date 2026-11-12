import { describe, expect, it } from 'vitest'
import { base64UrlToBytes, isIosDevice, pushStatus } from '~/utils/pushSupport'
import type { PushEnvironment } from '~/utils/pushSupport'

const ready: PushEnvironment = {
  hasPushApi: true,
  isIos: false,
  isStandalone: false,
  permission: 'default',
  serverEnabled: true,
  isSubscribed: false,
}

describe('pushStatus', () => {
  it('is off until the user allows notifications and the device is subscribed', () => {
    expect(pushStatus(ready)).toBe('off')
    expect(pushStatus({ ...ready, permission: 'granted' })).toBe('off')
    expect(pushStatus({ ...ready, permission: 'granted', isSubscribed: true })).toBe('on')
  })

  it('reports a blocked permission, even with a leftover subscription', () => {
    expect(pushStatus({ ...ready, permission: 'denied', isSubscribed: true })).toBe('blocked')
  })

  it('reports a server without keys before anything about this device', () => {
    expect(pushStatus({ ...ready, serverEnabled: false, permission: 'denied' })).toBe('server-off')
  })

  it('asks iPhone and iPad users to install the app first', () => {
    const safariTab = { ...ready, hasPushApi: false, isIos: true }
    expect(pushStatus(safariTab)).toBe('needs-install')
    // Installed but still no Push API: an iOS version before 16.4.
    expect(pushStatus({ ...safariTab, isStandalone: true })).toBe('unsupported')
    expect(pushStatus({ ...ready, hasPushApi: false })).toBe('unsupported')
  })
})

describe('isIosDevice', () => {
  it('recognises iPhones and iPads, including iPads that claim to be Macs', () => {
    expect(isIosDevice('Mozilla/5.0 (iPhone; CPU iPhone OS 17_4 like Mac OS X)', 5)).toBe(true)
    expect(isIosDevice('Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)', 5)).toBe(true)
    expect(isIosDevice('Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)', 0)).toBe(false)
    expect(isIosDevice('Mozilla/5.0 (Linux; Android 14)', 5)).toBe(false)
  })
})

describe('base64UrlToBytes', () => {
  it('decodes url-safe base64 without padding', () => {
    expect([...base64UrlToBytes('-_8')]).toEqual([251, 255])
    expect([...base64UrlToBytes('AQID')]).toEqual([1, 2, 3])
    const key = 'BGJ5ooB5kDkrAWZXGRjAgaCS7tiDNwMq38AHbApKnpJP0Fs1ykjqPHgfzq22mtPqPl6XSD5mK0PtZHRzzPUdNIU'
    expect(base64UrlToBytes(key)).toHaveLength(65)
    expect(base64UrlToBytes(key)[0]).toBe(4)
  })
})

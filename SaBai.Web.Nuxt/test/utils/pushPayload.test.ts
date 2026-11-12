import { describe, expect, it } from 'vitest'
import { DEFAULT_NOTIFICATION_TITLE, notificationTargetUrl, parsePushPayload } from '~/utils/pushPayload'

describe('parsePushPayload', () => {
  it('reads a JSON payload', () => {
    expect(parsePushPayload(JSON.stringify({
      title: 'Pharmacy',
      body: 'Order ready',
      url: '/patient/12',
      tag: 'order-7',
      requireInteraction: true,
    }))).toEqual({
      title: 'Pharmacy',
      body: 'Order ready',
      url: '/patient/12',
      tag: 'order-7',
      requireInteraction: true,
    })
  })

  it('always has a title to show', () => {
    expect(parsePushPayload(null).title).toBe(DEFAULT_NOTIFICATION_TITLE)
    expect(parsePushPayload('  ').title).toBe(DEFAULT_NOTIFICATION_TITLE)
    expect(parsePushPayload('{"body":"x"}')).toMatchObject({ title: DEFAULT_NOTIFICATION_TITLE, body: 'x' })
    expect(parsePushPayload('{"title":""}').title).toBe(DEFAULT_NOTIFICATION_TITLE)
  })

  it('shows a non-JSON body as the notification text', () => {
    expect(parsePushPayload('Order ready')).toEqual({ title: DEFAULT_NOTIFICATION_TITLE, body: 'Order ready' })
    expect(parsePushPayload('42')).toEqual({ title: DEFAULT_NOTIFICATION_TITLE, body: '42' })
    expect(parsePushPayload('[1]')).toEqual({ title: DEFAULT_NOTIFICATION_TITLE, body: '[1]' })
  })

  it('ignores fields of the wrong type', () => {
    expect(parsePushPayload('{"title":"T","body":5,"url":{},"requireInteraction":"yes"}')).toEqual({
      title: 'T',
      body: undefined,
      url: undefined,
      tag: undefined,
      requireInteraction: undefined,
    })
  })
})

describe('notificationTargetUrl', () => {
  const origin = 'https://sabai.example'

  it('resolves app paths against the origin', () => {
    expect(notificationTargetUrl('/patient/12?station=vitals', origin)).toBe(`${origin}/patient/12?station=vitals`)
    expect(notificationTargetUrl(`${origin}/pharmacy`, origin)).toBe(`${origin}/pharmacy`)
  })

  it('opens the home page when there is no usable in-app URL', () => {
    expect(notificationTargetUrl(undefined, origin)).toBe(`${origin}/`)
    expect(notificationTargetUrl('https://evil.example/x', origin)).toBe(`${origin}/`)
    expect(notificationTargetUrl('//evil.example/x', origin)).toBe(`${origin}/`)
    expect(notificationTargetUrl('javascript:alert(1)', origin)).toBe(`${origin}/`)
  })
})

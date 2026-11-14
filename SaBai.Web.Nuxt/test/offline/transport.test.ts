import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { isNetworkError } from '~/offline/network'
import { apiTransport, setTransportHooks } from '~/offline/transport'

type Request = Parameters<typeof apiTransport>[0]

function request(overrides: Partial<Request> = {}): Request {
  return { url: 'http://test/api/village-codes', method: 'GET', headers: { Accept: 'application/json' }, ...overrides }
}

describe('apiTransport', () => {
  const hooks = { onReachable: vi.fn(), onNetworkFailure: vi.fn(), onServerDate: vi.fn() }
  const fetchMock = vi.fn<typeof fetch>()

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock)
    setTransportHooks(hooks)
  })
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.clearAllMocks()
    fetchMock.mockReset()
  })

  it('merges options.headers into the request and records the server Date of a success', async () => {
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ ok: true }), {
      status: 200,
      headers: { 'Content-Type': 'application/json', 'Date': 'Sat, 14 Nov 2026 09:30:00 GMT' },
    }))
    const result = await apiTransport(request({
      method: 'PATCH',
      options: { headers: { 'If-Unmodified-Since': 'Fri, 13 Nov 2026 08:00:00 GMT' } },
    }))
    const sent = fetchMock.mock.calls[0]![0] as globalThis.Request
    expect(sent.headers.get('If-Unmodified-Since')).toBe('Fri, 13 Nov 2026 08:00:00 GMT')
    expect(sent.headers.get('Accept')).toBe('application/json')
    expect(result.data).toEqual({ ok: true })
    expect(result.status).toBe(200)
    expect(hooks.onServerDate).toHaveBeenCalledWith('Sat, 14 Nov 2026 09:30:00 GMT')
    expect(hooks.onReachable).toHaveBeenCalled()
  })

  it('reports an error response as reachable without recording its Date', async () => {
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ detail: 'bad' }), {
      status: 400,
      headers: { 'Content-Type': 'application/problem+json', 'Date': 'Sat, 14 Nov 2026 09:30:00 GMT' },
    }))
    const result = await apiTransport(request())
    expect(result.status).toBe(400)
    expect(result.data).toEqual({ detail: 'bad' })
    expect(hooks.onReachable).toHaveBeenCalled()
    expect(hooks.onServerDate).not.toHaveBeenCalled()
  })

  it('reports a proxy 502 as a network failure', async () => {
    fetchMock.mockResolvedValue(new Response('Bad Gateway', { status: 502 }))
    const result = await apiTransport(request())
    expect(result.status).toBe(502)
    expect(hooks.onNetworkFailure).toHaveBeenCalled()
    expect(hooks.onReachable).not.toHaveBeenCalled()
  })

  it('rethrows a failed fetch as a NetworkError', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'))
    const error = await apiTransport(request()).catch(e => e)
    expect(error.name).toBe('NetworkError')
    expect(isNetworkError(error)).toBe(true)
    expect(hooks.onNetworkFailure).toHaveBeenCalled()
  })

  it('times out a request that hangs', async () => {
    vi.useFakeTimers()
    try {
      fetchMock.mockImplementation((input) => {
        const signal = (input as globalThis.Request).signal
        return new Promise((_resolve, reject) => {
          signal.addEventListener('abort', () => reject(signal.reason))
        })
      })
      const pending = apiTransport(request()).catch(e => e)
      await vi.advanceTimersByTimeAsync(20_000)
      const error = await pending
      expect(error.name).toBe('TimeoutError')
      expect(isNetworkError(error)).toBe(true)
      expect(hooks.onNetworkFailure).toHaveBeenCalled()
    }
    finally {
      vi.useRealTimers()
    }
  })

  it('a caller abort is an AbortError, not a network failure', async () => {
    fetchMock.mockImplementation((input) => {
      const signal = (input as globalThis.Request).signal
      return new Promise((_resolve, reject) => {
        signal.addEventListener('abort', () => reject(signal.reason))
      })
    })
    const controller = new AbortController()
    const pending = apiTransport(request({ signal: controller.signal })).catch(e => e)
    controller.abort()
    const error = await pending
    expect(error.name).toBe('AbortError')
    expect(isNetworkError(error)).toBe(false)
    expect(hooks.onNetworkFailure).not.toHaveBeenCalled()
  })
})

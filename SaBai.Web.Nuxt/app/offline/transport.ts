import type { saBaiAPIEndpointsHealthcheckGetEndpoint } from '@sabai/sdk/clients'
import { GATEWAY_STATUSES, NetworkError } from './network'

/**
 * The `fetch` transport every @sabai/sdk call goes through (it is part of
 * `apiConfig`, which every call spreads). On top of what the SDK's default
 * transport does, it:
 * - merges `options.headers` into the request headers, which is how offline
 *   mutations pass `If-Unmodified-Since` (see `requestOptions`);
 * - times requests out, so a Wi-Fi network without an uplink fails fast
 *   instead of hanging (uploads get longer);
 * - rethrows a failed `fetch` as `NetworkError`, so it cannot be mistaken for
 *   a programming error;
 * - reports each outcome through {@link setTransportHooks}: whether the API
 *   was reached, and the server's `Date` header of every successful response.
 */

type SdkTransport = NonNullable<Parameters<typeof saBaiAPIEndpointsHealthcheckGetEndpoint>[0]['transport']>
type ResolvedRequest = Parameters<SdkTransport>[0]
type TransportResult = Awaited<ReturnType<SdkTransport>>

export const REQUEST_TIMEOUT_MS = 20_000
export const UPLOAD_TIMEOUT_MS = 120_000

export interface TransportHooks {
  /** The API answered (any status but a gateway error). */
  onReachable?: () => void
  /** The request did not reach the API (fetch failed, timed out, or a gateway error). */
  onNetworkFailure?: () => void
  /** A 2xx response's `Date` header. */
  onServerDate?: (date: string | null) => void
}

let hooks: TransportHooks = {}

/** Sets the callbacks the transport reports to (the offline runtime does this at start-up). */
export function setTransportHooks(next: TransportHooks): void {
  hooks = next
}

function headersToRecord(headers: HeadersInit | undefined): Record<string, string> {
  if (!headers) return {}
  return Object.fromEntries(new Headers(headers).entries())
}

function detectResponseType(contentType: string | null): string | undefined {
  if (!contentType) return undefined
  if (contentType.includes('text/event-stream')) return 'stream'
  if (contentType.includes('application/json') || contentType.includes('text/json')) return 'json'
  if (contentType.includes('text/')) return 'text'
  if (contentType.includes('image/') || contentType.includes('application/octet-stream')) return 'blob'
  return undefined
}

// Same decoding as the SDK's default transport.
async function parseResponse(response: Response, responseType?: string): Promise<unknown> {
  if (response.status === 204 || response.status === 205 || response.status === 304 || !response.body) return undefined
  const contentType = response.headers.get('Content-Type')
  const isJson = !!contentType && /[/+]json\b/i.test(contentType)
  const detected = responseType === 'stream' && (!response.ok || isJson) ? undefined : responseType
  switch (detected ?? detectResponseType(contentType)) {
    case 'text':
    case 'document':
      return response.text()
    case 'blob':
      return response.blob()
    case 'arraybuffer':
      return response.arrayBuffer()
    case 'stream':
      return response.body ?? undefined
    case 'json': {
      const body = await response.text()
      return body ? JSON.parse(body) : undefined
    }
  }
  const text = await response.text()
  if (!text) return undefined
  try {
    return JSON.parse(text)
  }
  catch {
    return text
  }
}

export const apiTransport: SdkTransport = async (request: ResolvedRequest): Promise<TransportResult> => {
  const { headers: extraHeaders, ...fetchOptions } = (request.options ?? {}) as RequestInit
  const isUpload = typeof FormData !== 'undefined' && request.body instanceof FormData

  // One controller for both the caller's signal and the timeout, so a timeout
  // surfaces as `TimeoutError` and a caller's abort as `AbortError`.
  const controller = new AbortController()
  const timeout = setTimeout(
    () => controller.abort(new DOMException('The request timed out.', 'TimeoutError')),
    isUpload ? UPLOAD_TIMEOUT_MS : REQUEST_TIMEOUT_MS,
  )
  const onCallerAbort = () => controller.abort(request.signal?.reason)
  if (request.signal?.aborted) onCallerAbort()
  else request.signal?.addEventListener('abort', onCallerAbort, { once: true })

  const init: RequestInit = {
    ...fetchOptions,
    method: request.method,
    headers: { ...request.headers, ...headersToRecord(extraHeaders) },
    body: request.body,
    signal: controller.signal,
  }
  if (request.credentials) init.credentials = request.credentials

  try {
    const nativeRequest = new Request(request.url, init)
    let response: Response
    try {
      response = await globalThis.fetch(nativeRequest)
    }
    catch (error) {
      if (controller.signal.aborted) {
        const reason: unknown = controller.signal.reason
        if (reason instanceof DOMException && reason.name === 'TimeoutError') hooks.onNetworkFailure?.()
        throw reason ?? error
      }
      hooks.onNetworkFailure?.()
      throw new NetworkError(undefined, { cause: error })
    }

    if (GATEWAY_STATUSES.has(response.status)) {
      hooks.onNetworkFailure?.()
    }
    else {
      hooks.onReachable?.()
      if (response.ok) hooks.onServerDate?.(response.headers.get('Date'))
    }

    let data: unknown
    try {
      data = await parseResponse(response, request.responseType)
    }
    catch (error) {
      // The connection dropped while reading the body.
      if (controller.signal.aborted) throw controller.signal.reason ?? error
      if (error instanceof TypeError) {
        hooks.onNetworkFailure?.()
        throw new NetworkError(undefined, { cause: error })
      }
      throw error
    }

    return {
      data,
      status: response.status,
      statusText: response.statusText,
      headers: response.headers,
      request: nativeRequest,
      response,
    } as TransportResult
  }
  finally {
    clearTimeout(timeout)
    request.signal?.removeEventListener('abort', onCallerAbort)
  }
}

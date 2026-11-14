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

type SdkTransport = NonNullable<NonNullable<Parameters<typeof saBaiAPIEndpointsHealthcheckGetEndpoint>[0]>['transport']>
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

type ResponseTypeOption = ResolvedRequest['responseType']

function isJsonMediaType(base: string | undefined): boolean {
  return !!base && (base === 'application/json' || base === 'text/json' || base.endsWith('+json'))
}

// Picks the parser as the SDK's default transport does: a string wins, a map is
// matched on the media type (exact, `type/*`, `*/*`), otherwise guessed from it.
function resolveResponseType(responseType: ResponseTypeOption, contentType: string | null): string | undefined {
  if (typeof responseType === 'string') return responseType
  const base = contentType?.split(';')[0]?.trim().toLowerCase() || undefined
  if (!base) return undefined
  const mapped = responseType?.[base] ?? responseType?.[`${base.split('/')[0]}/*`] ?? responseType?.['*/*']
  if (mapped) return mapped
  if (base === 'text/event-stream') return 'stream'
  if (isJsonMediaType(base)) return 'json'
  if (base.startsWith('text/')) return 'text'
  if (base === 'application/octet-stream' || base === 'application/pdf' || /^(image|audio|video)\//.test(base)) return 'blob'
  return undefined
}

// Same decoding as the SDK's default transport.
async function parseResponse(response: Response, responseType: ResponseTypeOption): Promise<unknown> {
  if (response.status === 204 || response.status === 205 || response.status === 304 || !response.body) return undefined
  const contentType = response.headers.get('Content-Type')
  const resolved = resolveResponseType(responseType, contentType)
  const isJson = isJsonMediaType(contentType?.split(';')[0]?.trim().toLowerCase())
  const detected = resolved === 'stream' && (!response.ok || isJson) ? resolveResponseType(undefined, contentType) : resolved
  switch (detected) {
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

    const contentType = response.headers.get('Content-Type')?.split(';')[0]?.trim().toLowerCase() || undefined
    return {
      data,
      status: response.status,
      statusText: response.statusText,
      headers: response.headers,
      contentType,
      request: nativeRequest,
      response,
    }
  }
  finally {
    clearTimeout(timeout)
    request.signal?.removeEventListener('abort', onCallerAbort)
  }
}

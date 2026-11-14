/**
 * Whether this device can reach SaBai.API right now.
 *
 * Driven by real request outcomes (the API transport reports every response
 * and every network failure) plus the browser's `online`/`offline` events.
 * An `offline` event is trusted; an `online` event only triggers a probe,
 * because `navigator.onLine` stays true on Wi-Fi without an uplink. While
 * offline, or while there are queued writes to send, a cheap probe of the
 * API's healthcheck runs on an interval.
 */

export interface ConnectivityState {
  offline: boolean
  /** Device time (ms) the server was last reached, if known. */
  lastReachableAt?: number
}

export interface ConnectivityOptions {
  /** Resolves true when the API answered. Must not throw. */
  probe: () => Promise<boolean>
  /** Whether to keep probing while online (e.g. queued writes are waiting). */
  shouldProbeWhileOnline?: () => boolean
  /** Called after each probe that reached the server and after each offline → online change. */
  onReachable?: () => void
  intervalMs?: number
  initialOffline?: boolean
}

export interface Connectivity {
  getState: () => ConnectivityState
  subscribe: (listener: (state: ConnectivityState) => void) => () => void
  /** A request got a response from the API (any status except a gateway error). */
  reportReachable: () => void
  /** A request failed to reach the API. */
  reportNetworkFailure: () => void
  /** Probes now; resolves whether the API was reached. Concurrent calls share one probe. */
  probeNow: () => Promise<boolean>
  /** Starts listening to window events and the probe interval. */
  start: (target?: Pick<Window, 'addEventListener' | 'removeEventListener'>) => void
  stop: () => void
}

export const PROBE_INTERVAL_MS = 15_000
export const PROBE_TIMEOUT_MS = 5_000

export function createConnectivity(options: ConnectivityOptions): Connectivity {
  let state: ConnectivityState = { offline: options.initialOffline ?? false }
  const listeners = new Set<(state: ConnectivityState) => void>()
  let probing: Promise<boolean> | undefined
  let timer: ReturnType<typeof setInterval> | undefined
  let detach: (() => void) | undefined

  function setState(next: ConnectivityState) {
    const wasOffline = state.offline
    state = next
    for (const listener of listeners) listener(state)
    if (wasOffline && !next.offline) options.onReachable?.()
  }

  function reportReachable() {
    if (state.offline) setState({ offline: false, lastReachableAt: Date.now() })
    else state = { ...state, lastReachableAt: Date.now() }
  }

  function reportNetworkFailure() {
    if (!state.offline) setState({ ...state, offline: true })
  }

  function probeNow(): Promise<boolean> {
    probing ??= (async () => {
      let ok = false
      try {
        ok = await options.probe()
      }
      catch {
        ok = false
      }
      if (ok) {
        const wasOffline = state.offline
        reportReachable()
        // reportReachable already notified on an offline → online change.
        if (!wasOffline) options.onReachable?.()
      }
      else {
        reportNetworkFailure()
      }
      return ok
    })().finally(() => {
      probing = undefined
    })
    return probing
  }

  function tick() {
    if (state.offline || options.shouldProbeWhileOnline?.()) void probeNow()
  }

  return {
    getState: () => state,
    subscribe(listener) {
      listeners.add(listener)
      return () => listeners.delete(listener)
    },
    reportReachable,
    reportNetworkFailure,
    probeNow,
    start(target = globalThis as unknown as Window) {
      if (detach) return
      const onOnline = () => void probeNow()
      const onOffline = () => reportNetworkFailure()
      target.addEventListener('online', onOnline)
      target.addEventListener('offline', onOffline)
      timer = setInterval(tick, options.intervalMs ?? PROBE_INTERVAL_MS)
      detach = () => {
        target.removeEventListener('online', onOnline)
        target.removeEventListener('offline', onOffline)
        clearInterval(timer)
      }
    },
    stop() {
      detach?.()
      detach = undefined
    },
  }
}

/** Probes SaBai.API's anonymous healthcheck through the `/api` proxy. */
export async function probeApiHealth(baseURL = '/api'): Promise<boolean> {
  const controller = new AbortController()
  const timeout = setTimeout(() => controller.abort(), PROBE_TIMEOUT_MS)
  try {
    const response = await fetch(`${baseURL}/healthcheck`, {
      cache: 'no-store',
      credentials: 'same-origin',
      signal: controller.signal,
    })
    return response.ok
  }
  catch {
    return false
  }
  finally {
    clearTimeout(timeout)
  }
}

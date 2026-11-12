/**
 * Calls to the API's push notification endpoints (`/push/*`).
 *
 * Written by hand because the generated SDK does not include them yet; like the
 * SDK calls they go through this app's `/api` proxy with the session cookie.
 * Errors carry `status` and `data`, so `getApiErrorMessage` reads them the same way.
 */
export interface PushConfig {
  /** False until the server has a VAPID key pair. */
  isEnabled: boolean
  /** VAPID public key (base64url) to subscribe with. */
  publicKey: string | null
  /** How many of your devices receive notifications. */
  deviceCount: number
}

export interface PushTestResult {
  sent: number
  removed: number
  failed: number
}

function request<T>(path: string, options: { method?: 'GET' | 'POST' | 'PUT', body?: Record<string, unknown>, signal?: AbortSignal } = {}) {
  return $fetch<T>(path, { baseURL: apiConfig.baseURL, credentials: apiConfig.credentials, ...options })
}

export function getPushConfig(signal?: AbortSignal) {
  return request<PushConfig>('/push/config', { signal })
}

/** Saves this device's subscription for the signed-in user. Safe to repeat. */
export function savePushSubscription(subscription: PushSubscription) {
  const { endpoint, keys } = subscription.toJSON()
  return request<void>('/push/subscriptions', { method: 'PUT', body: { endpoint, keys } })
}

export function removePushSubscription(endpoint: string) {
  return request<void>('/push/subscriptions/unsubscribe', { method: 'POST', body: { endpoint } })
}

export function sendTestPush() {
  return request<PushTestResult>('/push/test', { method: 'POST' })
}

/**
 * Whether this browser can receive notifications, and what stands in the way.
 * Framework-free so it can be unit tested; `usePushNotifications` feeds it.
 */
export type PushStatus =
  /** The browser has no Web Push at all. */
  | 'unsupported'
  /** iPhone/iPad: Web Push only exists once the app is on the Home Screen. */
  | 'needs-install'
  /** The server has no VAPID keys, so nothing can be sent. */
  | 'server-off'
  /** The user (or a browser policy) blocked notifications for this site. */
  | 'blocked'
  | 'off'
  | 'on'

export interface PushEnvironment {
  /** Service workers, the Push API and the Notification API all exist. */
  hasPushApi: boolean
  isIos: boolean
  /** Running as an installed app (Home Screen / Dock / installed PWA). */
  isStandalone: boolean
  permission: NotificationPermission
  serverEnabled: boolean
  /** This device holds a push subscription. */
  isSubscribed: boolean
}

export function pushStatus(env: PushEnvironment): PushStatus {
  if (!env.hasPushApi)
    return env.isIos && !env.isStandalone ? 'needs-install' : 'unsupported'
  if (!env.serverEnabled)
    return 'server-off'
  if (env.permission === 'denied')
    return 'blocked'
  return env.permission === 'granted' && env.isSubscribed ? 'on' : 'off'
}

/** iPhone, iPod, or iPad (which reports itself as a Mac with a touch screen). */
export function isIosDevice(userAgent: string, maxTouchPoints: number): boolean {
  return /iPhone|iPad|iPod/.test(userAgent) || (/Macintosh/.test(userAgent) && maxTouchPoints > 1)
}

/** Decodes the server's base64url VAPID key into the bytes `pushManager.subscribe` takes. */
export function base64UrlToBytes(value: string): Uint8Array<ArrayBuffer> {
  const base64 = value.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(value.length / 4) * 4, '=')
  const binary = atob(base64)
  const bytes = new Uint8Array(binary.length)
  for (let i = 0; i < binary.length; i++)
    bytes[i] = binary.charCodeAt(i)
  return bytes
}

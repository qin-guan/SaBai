/**
 * The push message contract between SaBai.API (sender) and the service worker
 * (`app/service-worker/sw.ts`), which turns each message into a notification.
 *
 * The API sends this as the JSON body of a Web Push message. Only `title` is
 * needed; a body that is not JSON is shown as the notification text.
 */
export interface PushPayload {
  title: string
  body?: string
  /** Same-origin path to open when the notification is tapped, e.g. `/patient/12`. */
  url?: string
  /** Notifications with the same tag replace each other instead of stacking. */
  tag?: string
  /** Keep the notification on screen until the user acts on it (desktop). */
  requireInteraction?: boolean
}

export const DEFAULT_NOTIFICATION_TITLE = "Sa'Bai"

/**
 * Reads a push message body. Never throws and always returns something to
 * show: browsers require every push to produce a notification, and Safari
 * drops the subscription of an app that receives pushes silently.
 */
export function parsePushPayload(text: string | null | undefined): PushPayload {
  const raw = text?.trim()
  if (!raw)
    return { title: DEFAULT_NOTIFICATION_TITLE }

  let data: unknown
  try {
    data = JSON.parse(raw)
  }
  catch {
    return { title: DEFAULT_NOTIFICATION_TITLE, body: raw }
  }
  if (typeof data !== 'object' || data === null || Array.isArray(data))
    return { title: DEFAULT_NOTIFICATION_TITLE, body: raw }

  const fields = data as Record<string, unknown>
  const str = (value: unknown) => (typeof value === 'string' && value.trim() ? value : undefined)
  return {
    title: str(fields.title) ?? DEFAULT_NOTIFICATION_TITLE,
    body: str(fields.body),
    url: str(fields.url),
    tag: str(fields.tag),
    requireInteraction: fields.requireInteraction === true ? true : undefined,
  }
}

/**
 * The URL a tapped notification opens. Anything that does not resolve to this
 * app's origin opens the home page, so a push can never send staff elsewhere.
 */
export function notificationTargetUrl(url: string | undefined, origin: string): string {
  const home = new URL('/', origin).href
  if (!url)
    return home
  try {
    const target = new URL(url, origin)
    return target.origin === new URL(origin).origin ? target.href : home
  }
  catch {
    return home
  }
}

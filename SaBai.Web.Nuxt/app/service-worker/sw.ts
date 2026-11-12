/// <reference lib="webworker" />
import { notificationTargetUrl, parsePushPayload } from '../utils/pushPayload'

/**
 * Service worker for the installed app (PWA). Its job is notifications: it
 * shows a notification for every Web Push message, also while the app is
 * closed, and opens the app on the right page when one is tapped.
 *
 * It has no `fetch` handler and caches nothing, so pages, the `/api` proxy and
 * the session cookie behave exactly as they do without it.
 */
declare const self: ServiceWorkerGlobalScope

// A new version takes over at once: there is no cache to keep consistent.
self.addEventListener('install', () => {
  void self.skipWaiting()
})
self.addEventListener('activate', (event) => {
  event.waitUntil(self.clients.claim())
})

self.addEventListener('push', (event) => {
  const payload = parsePushPayload(event.data?.text())
  event.waitUntil(
    self.registration.showNotification(payload.title, {
      body: payload.body,
      tag: payload.tag,
      requireInteraction: payload.requireInteraction,
      icon: '/pwa-192x192.png',
      badge: '/notification-badge.png',
      data: { url: payload.url },
    }),
  )
})

self.addEventListener('notificationclick', (event) => {
  event.notification.close()
  const target = notificationTargetUrl(event.notification.data?.url, self.location.origin)

  event.waitUntil((async () => {
    const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true })
    // Reuse an open app window rather than stacking a second one.
    const open = windows.find(client => client.url === target) ?? windows[0]
    if (open) {
      await open.focus()
      if (open.url !== target)
        await open.navigate(target)
      return
    }
    await self.clients.openWindow(target)
  })())
})

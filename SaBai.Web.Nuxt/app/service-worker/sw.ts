/// <reference lib="webworker" />
import { ExpirationPlugin } from 'workbox-expiration'
import { cleanupOutdatedCaches, createHandlerBoundToURL, precacheAndRoute } from 'workbox-precaching'
import { NavigationRoute, registerRoute } from 'workbox-routing'
import { CacheFirst, StaleWhileRevalidate } from 'workbox-strategies'
import { notificationTargetUrl, parsePushPayload } from '../utils/pushPayload'

/**
 * Service worker for the offline-first PWA:
 * 1. Precaches the app shell, fonts, and WASM SQLite runtime via Workbox.
 * 2. Serves cached app routes when offline (excluding `/api` and `/powersync`).
 * 3. Caches patient photos (`StaleWhileRevalidate`) and static fonts (`CacheFirst`).
 * 4. Handles Web Push notifications and notification clicks.
 */
declare const self: ServiceWorkerGlobalScope & {
  __WB_MANIFEST: Array<{ url: string; revision: string | null }>
}

cleanupOutdatedCaches()
precacheAndRoute(self.__WB_MANIFEST ?? [])

// SPA navigation fallback to the precached shell, never intercepting API or PowerSync.
try {
  registerRoute(
    new NavigationRoute(createHandlerBoundToURL('/index.html'), {
      denylist: [/^\/api\//, /^\/powersync\//],
    }),
  )
} catch {
  // In dev mode without a precached /index.html, navigation falls through to network.
}

// Cache fonts & static icons/images.
registerRoute(
  ({ request }) => request.destination === 'font' || request.destination === 'style',
  new CacheFirst({
    cacheName: 'sabai-static-assets',
    plugins: [
      new ExpirationPlugin({
        maxEntries: 80,
        maxAgeSeconds: 30 * 24 * 60 * 60,
      }),
    ],
  }),
)

// Cache patient photos & uploaded file previews for offline viewing.
registerRoute(
  ({ url, request }) =>
    request.method === 'GET'
    && (/^\/api\/patients\/[^/]+\/photo$/.test(url.pathname)
      || /^\/api\/files\/[^/]+\/content$/.test(url.pathname)),
  new StaleWhileRevalidate({
    cacheName: 'sabai-patient-media',
    plugins: [
      new ExpirationPlugin({
        maxEntries: 250,
        maxAgeSeconds: 7 * 24 * 60 * 60,
      }),
    ],
  }),
)

self.addEventListener('message', (event) => {
  if (event.data?.type === 'SKIP_WAITING') {
    void self.skipWaiting()
  }
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

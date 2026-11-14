/**
 * Reverse proxy to the self-hosted PowerSync Service.
 *
 * The browser connects to same-origin `/powersync/*` and this Nitro route
 * forwards HTTP and streaming sync requests to `runtimeConfig.powersyncUrl`
 * with the `/powersync` prefix stripped.
 */
const PREFIX = '/powersync'

export default defineEventHandler((event) => {
  const powersyncUrl = (useRuntimeConfig(event).powersyncUrl as string).replace(/\/+$/, '')
  const path = event.path.startsWith(PREFIX) ? event.path.slice(PREFIX.length) : event.path

  return proxyRequest(event, powersyncUrl + (path.startsWith('/') ? path : `/${path}`), {
    streamRequest: true,
  })
})

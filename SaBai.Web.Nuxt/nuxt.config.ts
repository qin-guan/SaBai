import tailwindcss from '@tailwindcss/vite'

// The SPA shell is rendered by the Nitro server, not emitted as a file, so the
// service worker precaches it from `/` under a revision that changes per build.
const appShellRevision = Date.now().toString(36)

// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  modules: ['@nuxt/a11y', '@nuxt/fonts', '@nuxt/hints', '@nuxt/icon/module', '@nuxt/image', '@nuxt/scripts', 'shadcn-nuxt', '@vite-pwa/nuxt'],
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },
  // The app is a pure client of SaBai.API: auth is an HttpOnly session cookie
  // set by the API through the /api proxy,
  // patient photos are fetched as authenticated blobs and the webcam is
  // browser-only, so it renders on the client like SaBai.Web does.
  ssr: false,
  css: ['~/assets/css/main.css'],
  // App components are referenced by file name (<PatientBanner>, <AppPanel>…).
  // shadcn components in components/ui are imported explicitly.
  components: [{ path: '~/components', pathPrefix: false, ignore: ['ui/**'] }],
  runtimeConfig: {
    // Server-only URL of SaBai.API, which may sit on an internal network the
    // browser cannot reach. The browser calls this server under /api and
    // server/api/[...path].ts proxies to it. Override with NUXT_API_URL.
    apiUrl: 'http://localhost:5250',
  },
  app: {
    head: {
      title: "Sa'Bai",
      htmlAttrs: { lang: 'en' },
      meta: [
        { name: 'viewport', content: 'width=device-width, initial-scale=1, viewport-fit=cover' },
        { name: 'theme-color', content: '#1f3a5f' },
        { name: 'mobile-web-app-capable', content: 'yes' },
        { name: 'apple-mobile-web-app-title', content: "Sa'Bai" },
      ],
      link: [
        { rel: 'icon', href: '/favicon.ico', sizes: 'any' },
        { rel: 'apple-touch-icon', href: '/apple-touch-icon.png' },
        { rel: 'manifest', href: '/manifest.webmanifest' },
      ],
    },
  },
  // Installable PWA: the service worker precaches the app shell so the app opens
  // offline. Data offline is the Vue Query cache persisted in IndexedDB plus the
  // outbox of queued writes (app/offline).
  pwa: {
    strategies: 'injectManifest',
    srcDir: 'service-worker',
    filename: 'sw.ts',
    registerType: 'prompt',
    injectManifest: {
      globPatterns: ['**/*.{js,css,html,ico,png,svg,woff2}'],
      additionalManifestEntries: [{ url: '/', revision: appShellRevision }],
    },
    manifest: {
      id: '/',
      name: "Sa'Bai",
      short_name: "Sa'Bai",
      description: "Sa'Bai clinic patient records",
      lang: 'en',
      start_url: '/',
      scope: '/',
      display: 'standalone',
      theme_color: '#1f3a5f',
      background_color: '#eef1f5',
      icons: [
        { src: '/pwa-192x192.png', sizes: '192x192', type: 'image/png' },
        { src: '/pwa-512x512.png', sizes: '512x512', type: 'image/png' },
        { src: '/pwa-maskable-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
      ],
    },
    // Run the worker under `pnpm dev` too, so notifications and offline shell can be tested locally.
    devOptions: { enabled: true, type: 'module' },
  },
  fonts: {
    defaults: {
      weights: [400, 500, 600, 700],
    },
    families: [
      {
        name: 'Noto Sans Khmer',
        subsets: ['khmer'],
        styles: ['normal'],
        fallbacks: [],
      },
    ],
  },
  vite: {
    plugins: [
      tailwindcss(),
    ],
  },
  shadcn: {
    /**
     * Prefix for all the imported component.
     * @default "Ui"
     */
    prefix: '',
    /**
     * Directory that the component lives in.
     * Will respect the Nuxt aliases.
     * @link https://nuxt.com/docs/api/nuxt-config#alias
     * @default "@/components/ui"
     */
    componentDir: '@/components/ui'
  }
})

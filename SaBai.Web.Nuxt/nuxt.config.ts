import tailwindcss from '@tailwindcss/vite'

// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  modules: ['@nuxt/a11y', '@nuxt/fonts', '@nuxt/hints', '@nuxt/icon/module', '@nuxt/image', '@nuxt/scripts', 'shadcn-nuxt'],
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
      ],
    },
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

import { PowerSyncDatabase } from '@powersync/web'
import { createPowerSyncPlugin } from '@powersync/vue'
import { toast } from 'vue-sonner'
import { SaBaiPowerSyncConnector } from '~/db/connector'
import { AppSchema } from '~/db/schema'

export default defineNuxtPlugin(async (nuxtApp) => {
  // Request persistent storage so the browser does not evict the local SQLite database.
  if (navigator.storage?.persist) {
    void navigator.storage.persist().catch(() => {})
  }

  const db = new PowerSyncDatabase({
    schema: AppSchema,
    database: {
      dbFilename: 'sabai.db',
    },
  })

  setPowerSyncInstance(db)
  nuxtApp.vueApp.use(createPowerSyncPlugin({ database: db }))

  const connector = new SaBaiPowerSyncConnector((result) => {
    recordUploadResult(result)
    if (result.rejections.length > 0) {
      toast.warning(
        `${result.rejections.length} offline change(s) could not be applied and need review.`,
      )
    } else if (result.conflicts > 0) {
      toast.info(
        `${result.conflicts} sync conflict(s) were resolved with last-write-wins. Review in Sync Attention.`,
      )
    }
  })

  db.registerListener({
    statusChanged: (status) => {
      updateSyncStatusFromSdk(status)
    },
  })

  // Refresh pending count immediately on startup.
  void refreshPendingCrudCount()

  let connectedOnce = false
  const ensureConnected = () => {
    const { $queryClient } = nuxtApp
    const user = $queryClient?.getQueryData(queryKeys.currentUser())
    if (!user || !navigator.onLine) return
    if (!connectedOnce || !db.connected) {
      connectedOnce = true
      void db.connect(connector).catch((err) => {
        console.warn('PowerSync connect deferred:', err)
      })
      void refreshAttentionCount()
    }
  }

  window.addEventListener('online', ensureConnected)

  nuxtApp.hook('app:mounted', () => {
    ensureConnected()
  })

  return {
    provide: {
      powersync: db,
      powersyncConnector: connector,
      connectPowerSync: ensureConnected,
    },
  }
})

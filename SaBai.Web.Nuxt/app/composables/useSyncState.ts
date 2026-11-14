import type { PowerSyncDatabase } from '@powersync/web'
import { useOnline } from '@vueuse/core'
import type { SyncUploadResult } from '~/db/connector'

const pendingCrudCount = ref(0)
const attentionCount = ref(0)
const connected = ref(false)
const connecting = ref(false)
const downloading = ref(false)
const uploading = ref(false)
const hasSynced = ref(false)
const lastSyncedAt = ref<Date | null>(null)
const lastError = ref<string | null>(null)
const lastUploadResult = ref<SyncUploadResult | null>(null)

let dbInstance: PowerSyncDatabase | null = null

export function setPowerSyncInstance(db: PowerSyncDatabase) {
  dbInstance = db
}

export function getPowerSyncInstance(): PowerSyncDatabase {
  if (!dbInstance) {
    throw new Error('PowerSync database has not been initialized yet.')
  }
  return dbInstance
}

export async function refreshPendingCrudCount(): Promise<number> {
  if (!dbInstance) return 0
  try {
    const row = await dbInstance.getOptional<{ c: number }>('SELECT COUNT(*) AS c FROM ps_crud')
    pendingCrudCount.value = Number(row?.c ?? 0)
    return pendingCrudCount.value
  } catch {
    return pendingCrudCount.value
  }
}

export async function refreshAttentionCount(): Promise<number> {
  if (typeof navigator !== 'undefined' && !navigator.onLine) {
    return attentionCount.value
  }
  try {
    const res = await fetch('/api/sync/attention', { credentials: 'include' })
    if (!res.ok) return attentionCount.value
    const data = (await res.json()) as {
      conflicts: unknown[]
      rejections: unknown[]
      duplicatePatients: unknown[]
    }
    attentionCount.value =
      (data.conflicts?.length ?? 0)
      + (data.rejections?.length ?? 0)
      + (data.duplicatePatients?.length ?? 0)
    return attentionCount.value
  } catch {
    return attentionCount.value
  }
}

export function updateSyncStatusFromSdk(status: {
  connected: boolean
  connecting: boolean
  lastSyncedAt?: Date
  hasSynced?: boolean
  dataFlowStatus?: {
    downloading?: boolean
    uploading?: boolean
    downloadError?: Error
    uploadError?: Error
  }
}) {
  connected.value = status.connected
  connecting.value = status.connecting
  downloading.value = Boolean(status.dataFlowStatus?.downloading)
  uploading.value = Boolean(status.dataFlowStatus?.uploading)
  hasSynced.value = Boolean(status.hasSynced)
  if (status.lastSyncedAt) {
    lastSyncedAt.value = status.lastSyncedAt
  }
  const err = status.dataFlowStatus?.uploadError ?? status.dataFlowStatus?.downloadError
  lastError.value = err ? err.message : null
  void refreshPendingCrudCount()
}

export function recordUploadResult(result: SyncUploadResult) {
  lastUploadResult.value = result
  if (result.conflicts > 0 || result.rejections.length > 0) {
    void refreshAttentionCount()
  }
}

export function useSyncState() {
  const isOnline = useOnline()

  const statusLabel = computed(() => {
    if (!isOnline.value) {
      return pendingCrudCount.value > 0
        ? `Offline (${pendingCrudCount.value} queued)`
        : 'Offline'
    }
    if (uploading.value) return 'Syncing changes…'
    if (downloading.value) return 'Downloading…'
    if (connecting.value) return 'Connecting…'
    if (pendingCrudCount.value > 0) return `Pending (${pendingCrudCount.value})`
    if (connected.value) return 'Synced'
    return 'Idle'
  })

  return {
    isOnline,
    connected: readonly(connected),
    connecting: readonly(connecting),
    downloading: readonly(downloading),
    uploading: readonly(uploading),
    hasSynced: readonly(hasSynced),
    lastSyncedAt: readonly(lastSyncedAt),
    lastError: readonly(lastError),
    pendingCrudCount: readonly(pendingCrudCount),
    attentionCount: readonly(attentionCount),
    lastUploadResult: readonly(lastUploadResult),
    statusLabel,
    refreshPendingCrudCount,
    refreshAttentionCount,
  }
}

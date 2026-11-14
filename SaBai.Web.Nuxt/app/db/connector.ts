import type { AbstractPowerSyncDatabase, PowerSyncBackendConnector, PowerSyncCredentials } from '@powersync/web'

export interface SyncRejectionSummary {
  opId: number | null
  table: string
  recordId: string
  reason: string
}

export interface SyncUploadResult {
  applied: number
  conflicts: number
  rejections: SyncRejectionSummary[]
}

export class SaBaiPowerSyncConnector implements PowerSyncBackendConnector {
  constructor(private readonly onUploadResult?: (result: SyncUploadResult) => void) {}

  async fetchCredentials(): Promise<PowerSyncCredentials> {
    const res = await fetch('/api/sync/token', {
      credentials: 'include',
    })
    if (!res.ok) {
      throw new Error(`Failed to fetch PowerSync credentials (${res.status})`)
    }
    const data = (await res.json()) as {
      token: string
      endpoint: string
      expiresAt: string
    }
    const endpoint = data.endpoint.startsWith('/')
      ? `${window.location.origin}${data.endpoint}`
      : data.endpoint

    return {
      endpoint,
      token: data.token,
      expiresAt: new Date(data.expiresAt),
    }
  }

  async uploadData(database: AbstractPowerSyncDatabase): Promise<void> {
    const tx = await database.getNextCrudTransaction()
    if (!tx) return

    const ops = tx.crud.map(op => ({
      op: op.op,
      type: op.table,
      id: op.id,
      data: op.opData ?? null,
      opId: (op as { op_id?: number }).op_id ?? null,
      baseVersion:
        typeof op.previousValues?.version === 'number' ? op.previousValues.version : null,
    }))

    const res = await fetch('/api/sync/upload', {
      method: 'POST',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ ops }),
    })

    if (!res.ok) {
      throw new Error(`Sync upload failed (${res.status})`)
    }

    const result = (await res.json()) as SyncUploadResult
    this.onUploadResult?.(result)
    await tx.complete()
  }
}

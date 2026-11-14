<script setup lang="ts">
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'

interface ConflictItem {
  id: string
  table: string
  recordId: string
  field: string
  previousValue: string | null
  incomingValue: string | null
  winningValue: string | null
  occurredAt: string
}

interface RejectionItem {
  id: string
  table: string
  recordId: string
  operation: string
  payloadJson: string
  reason: string
  occurredAt: string
}

interface DuplicatePatientSummary {
  id: string
  displayId: string
  name: string
  identificationNumber: string | null
  dateOfBirth: string
  gender: string
  createdAt: string
  visitCount: number
}

interface DuplicatePatientGroup {
  matchReason: string
  patients: DuplicatePatientSummary[]
}

interface AttentionResponse {
  conflicts: ConflictItem[]
  rejections: RejectionItem[]
  duplicatePatients: DuplicatePatientGroup[]
}

const {
  isOnline,
  connected,
  statusLabel,
  lastSyncedAt,
  pendingCrudCount,
  lastError,
  refreshAttentionCount,
} = useSyncState()

const loading = ref(false)
const attention = ref<AttentionResponse>({
  conflicts: [],
  rejections: [],
  duplicatePatients: [],
})
const selectedPrimary = ref<Record<number, string>>({})

async function loadAttention() {
  if (!isOnline.value) return
  loading.value = true
  try {
    const res = await fetch('/api/sync/attention', { credentials: 'include' })
    if (!res.ok) throw new Error(`Failed to load sync attention (${res.status})`)
    const data = (await res.json()) as AttentionResponse
    attention.value = data
    data.duplicatePatients.forEach((grp, idx) => {
      if (!selectedPrimary.value[idx] && grp.patients[0]) {
        selectedPrimary.value[idx] = grp.patients[0].id
      }
    })
    await refreshAttentionCount()
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to load sync status.')
  } finally {
    loading.value = false
  }
}

async function resolveConflict(id: string) {
  const res = await fetch(`/api/sync/conflicts/${id}/resolve`, {
    method: 'POST',
    credentials: 'include',
  })
  if (!res.ok) {
    toast.error('Failed to resolve conflict.')
    return
  }
  toast.success('Conflict marked as reviewed.')
  await loadAttention()
}

async function dismissRejection(id: string) {
  const res = await fetch(`/api/sync/rejections/${id}/dismiss`, {
    method: 'POST',
    credentials: 'include',
  })
  if (!res.ok) {
    toast.error('Failed to dismiss rejection.')
    return
  }
  toast.success('Rejection dismissed.')
  await loadAttention()
}

async function mergeDuplicates(groupIndex: number, group: DuplicatePatientGroup) {
  const targetId = selectedPrimary.value[groupIndex]
  if (!targetId) return
  const sources = group.patients.filter(p => p.id !== targetId)
  if (sources.length === 0) return

  for (const src of sources) {
    const res = await fetch('/api/sync/patients/merge', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        sourcePatientId: src.id,
        targetPatientId: targetId,
      }),
    })
    if (!res.ok) {
      toast.error(`Failed to merge ${src.name}.`)
      return
    }
  }
  toast.success('Duplicate patient records merged.')
  await loadAttention()
}

onMounted(() => {
  void loadAttention()
})
</script>

<template>
  <div class="mx-auto max-w-4xl space-y-6">
    <PageHeader
      title="Sync & Offline Status"
      subtitle="Local database replication, queued offline edits, conflicts, and duplicate patient review"
    >
      <template #actions>
        <Button :disabled="!isOnline || loading" variant="outline" size="sm" @click="loadAttention">
          Refresh
        </Button>
      </template>
    </PageHeader>

    <AppPanel title="Device sync health">
      <dl class="grid grid-cols-2 gap-4 text-sm sm:grid-cols-4">
        <div>
          <dt class="text-muted-foreground">Status</dt>
          <dd class="font-semibold">{{ statusLabel }}</dd>
        </div>
        <div>
          <dt class="text-muted-foreground">PowerSync stream</dt>
          <dd class="font-semibold">{{ connected ? 'Connected' : 'Disconnected' }}</dd>
        </div>
        <div>
          <dt class="text-muted-foreground">Queued offline edits</dt>
          <dd class="font-semibold">{{ pendingCrudCount }}</dd>
        </div>
        <div>
          <dt class="text-muted-foreground">Last synced</dt>
          <dd class="font-semibold">
            {{ lastSyncedAt ? lastSyncedAt.toLocaleTimeString() : 'Not yet' }}
          </dd>
        </div>
      </dl>
      <p v-if="lastError" class="mt-3 text-xs text-destructive">
        Last sync error: {{ lastError }}
      </p>
    </AppPanel>

    <AppPanel v-if="!isOnline" title="Offline mode">
      <p class="text-sm text-muted-foreground">
        You are currently offline. You can continue reading and editing patient records; changes are saved to this device and will upload automatically when connectivity returns. Connect to the server to review conflicts, rejections, and potential duplicate patients.
      </p>
    </AppPanel>

    <template v-else>
      <AppPanel :title="`Sync rejections (${attention.rejections.length})`">
        <p v-if="attention.rejections.length === 0" class="text-sm text-muted-foreground">
          No rejected offline changes.
        </p>
        <ul v-else class="divide-y">
          <li v-for="r in attention.rejections" :key="r.id" class="flex flex-col gap-2 py-3 sm:flex-row sm:items-center sm:justify-between">
            <div class="min-w-0 space-y-1 text-sm">
              <p class="font-medium">
                {{ r.operation }} on <code class="text-xs">{{ r.table }}</code>
                <span class="text-xs text-muted-foreground">({{ r.recordId }})</span>
              </p>
              <p class="text-destructive">{{ r.reason }}</p>
              <p class="truncate text-xs text-muted-foreground">{{ r.payloadJson }}</p>
            </div>
            <Button size="sm" variant="outline" @click="dismissRejection(r.id)">
              Dismiss
            </Button>
          </li>
        </ul>
      </AppPanel>

      <AppPanel :title="`Same-field conflicts (${attention.conflicts.length})`">
        <p v-if="attention.conflicts.length === 0" class="text-sm text-muted-foreground">
          No unresolved field conflicts.
        </p>
        <ul v-else class="divide-y">
          <li v-for="c in attention.conflicts" :key="c.id" class="flex flex-col gap-2 py-3 sm:flex-row sm:items-center sm:justify-between">
            <div class="space-y-1 text-sm">
              <p class="font-medium">
                <code class="text-xs">{{ c.table }}.{{ c.field }}</code>
                <span class="text-xs text-muted-foreground">({{ c.recordId }})</span>
              </p>
              <p class="text-xs text-muted-foreground">
                Previous: <span class="line-through">{{ c.previousValue ?? 'empty' }}</span>
                → Kept: <span class="font-semibold text-foreground">{{ c.winningValue ?? 'empty' }}</span>
              </p>
            </div>
            <Button size="sm" variant="outline" @click="resolveConflict(c.id)">
              Mark reviewed
            </Button>
          </li>
        </ul>
      </AppPanel>

      <AppPanel :title="`Possible duplicate patients (${attention.duplicatePatients.length})`">
        <p v-if="attention.duplicatePatients.length === 0" class="text-sm text-muted-foreground">
          No duplicate patient candidates detected.
        </p>
        <div v-else class="space-y-4">
          <div
            v-for="(grp, idx) in attention.duplicatePatients"
            :key="idx"
            class="rounded-md border p-3"
          >
            <p class="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              {{ grp.matchReason }}
            </p>
            <div class="space-y-2">
              <label
                v-for="p in grp.patients"
                :key="p.id"
                class="flex items-center gap-2 text-sm"
              >
                <input
                  v-model="selectedPrimary[idx]"
                  type="radio"
                  :name="`dup-${idx}`"
                  :value="p.id"
                >
                <span class="font-medium">{{ p.name }}</span>
                <span class="text-xs text-muted-foreground">
                  ({{ p.displayId }} · {{ p.visitCount }} visit(s) · DOB {{ p.dateOfBirth.slice(0, 10) }})
                </span>
              </label>
            </div>
            <div class="mt-3 flex justify-end">
              <Button size="sm" @click="mergeDuplicates(idx, grp)">
                Merge into selected primary
              </Button>
            </div>
          </div>
        </div>
      </AppPanel>
    </template>
  </div>
</template>

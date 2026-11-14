<script setup lang="ts">
import {
  PhArrowClockwise,
  PhArrowsClockwise,
  PhCheckCircle,
  PhCloudArrowDown,
  PhCloudCheck,
  PhCloudSlash,
  PhTrash,
  PhWarning,
} from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import type { OutboxItem } from '~/offline/outbox'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'

useHead({ title: "Sync · Sa'Bai" })

const {
  isOffline,
  replaying,
  pendingCount,
  attentionCount,
  lastSyncedAt,
  items,
  statusLabel,
  prefetch,
  syncNow,
  downloadForOffline,
  retry,
  overwrite,
  discard,
} = useSyncState()

const pendingItems = computed(() => items.value.filter(item => item.status === 'queued'))
const attentionItems = computed(() => items.value.filter(item => item.status !== 'queued'))

const syncing = ref(false)
async function onSyncNow() {
  syncing.value = true
  try {
    const reached = await syncNow()
    if (!reached) toast.warning('The server cannot be reached. Changes stay on this device until it can.')
    else if (pendingCount.value === 0 && attentionCount.value === 0) toast.success('Everything is synced.')
  }
  finally {
    syncing.value = false
  }
}

async function onDownload() {
  const result = await downloadForOffline()
  if (result.failed.length > 0) toast.warning(`Some data could not be downloaded (${result.failed.join(', ')}). Try again when the connection is better.`)
  else toast.success('Downloaded for offline use.')
}

const busyId = ref<string | null>(null)
async function act(item: OutboxItem, action: (id: string) => Promise<unknown>) {
  busyId.value = item.id
  try {
    await action(item.id)
  }
  catch (err) {
    console.error(err)
    toast.error('That did not work. Try again.')
  }
  finally {
    busyId.value = null
  }
}

const discarding = ref<OutboxItem | null>(null)
async function confirmDiscard() {
  const item = discarding.value
  if (!item) return
  await act(item, discard)
  discarding.value = null
  toast.success('Change discarded.')
}

const timeFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' })
function formatTime(value: string | Date | null | undefined) {
  return value ? timeFormat.format(new Date(value)) : 'Not yet'
}

const prefetchPercent = computed(() =>
  prefetch.value.total > 0 ? Math.round((prefetch.value.done / prefetch.value.total) * 100) : 0,
)
</script>

<template>
  <div class="flex flex-col gap-4">
    <PageHeader
      title="Sync"
      description="Changes saved on this device and the connection to the server."
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Settings', to: paths.settings() }, { label: 'Sync' }]"
    />

    <!-- Connection status -->
    <div class="flex flex-wrap items-center justify-between gap-3 border border-border bg-card p-3 text-sm" role="status">
      <div class="flex flex-wrap items-center gap-x-3 gap-y-1">
        <Badge v-if="isOffline" variant="outline" class="border-warning/40 bg-warning-subtle text-warning">
          <PhCloudSlash aria-hidden="true" /> Offline
        </Badge>
        <Badge v-else variant="outline" class="border-success/40 bg-success-subtle text-success">
          <PhCloudCheck aria-hidden="true" /> Connected
        </Badge>
        <span class="font-semibold">{{ statusLabel }}</span>
        <span class="text-xs text-muted-foreground touch:text-sm">Last synced: {{ formatTime(lastSyncedAt) }}</span>
      </div>
      <Button size="sm" :disabled="syncing || replaying" @click="onSyncNow">
        <PhArrowsClockwise :class="(syncing || replaying) && 'animate-spin motion-reduce:animate-none'" aria-hidden="true" />
        {{ syncing || replaying ? 'Syncing…' : 'Sync now' }}
      </Button>
    </div>

    <!-- Conflicts and rejections -->
    <AppPanel v-if="attentionItems.length > 0" :title="`Needs attention (${attentionItems.length})`" flush>
      <ul class="divide-y divide-border">
        <li
          v-for="item in attentionItems"
          :key="item.id"
          class="flex flex-col gap-2 px-3 py-3 sm:flex-row sm:items-start sm:justify-between"
          data-testid="attention-item"
        >
          <div class="min-w-0 space-y-1">
            <p class="flex flex-wrap items-center gap-2 font-semibold">
              <PhWarning class="size-4 shrink-0 text-warning" aria-hidden="true" />
              {{ item.label }}
              <Badge v-if="item.status === 'conflict'" variant="outline" class="border-warning/40 bg-warning-subtle text-warning">
                Changed by someone else
              </Badge>
              <Badge v-else variant="outline" class="border-destructive/40 bg-destructive-subtle text-destructive">
                Not accepted
              </Badge>
            </p>
            <p class="text-xs text-muted-foreground touch:text-sm">
              Saved {{ formatTime(item.queuedAt) }}
            </p>
            <p v-if="item.error" class="text-xs touch:text-sm" :class="item.status === 'conflict' ? 'text-warning' : 'text-destructive'">
              {{ item.error }}
            </p>
            <p v-if="item.status === 'conflict'" class="text-xs text-muted-foreground touch:text-sm">
              <strong>Overwrite</strong> replaces their change with yours. <strong>Discard</strong> keeps theirs and drops yours.
            </p>
          </div>
          <div class="flex shrink-0 flex-wrap gap-1 sm:justify-end">
            <Button
              v-if="item.status === 'conflict'"
              size="sm"
              variant="outline"
              :disabled="busyId === item.id"
              @click="act(item, overwrite)"
            >
              <PhCheckCircle aria-hidden="true" /> Overwrite
            </Button>
            <Button size="sm" variant="outline" :disabled="busyId === item.id" @click="act(item, retry)">
              <PhArrowClockwise aria-hidden="true" /> Retry
            </Button>
            <Button size="sm" variant="destructive" :disabled="busyId === item.id" @click="discarding = item">
              <PhTrash aria-hidden="true" /> Discard
            </Button>
          </div>
        </li>
      </ul>
    </AppPanel>

    <!-- Pending changes -->
    <AppPanel :title="`Waiting to be sent (${pendingItems.length})`" flush>
      <StateMessage
        v-if="pendingItems.length === 0"
        message="Nothing waiting. Every change made on this device has been sent."
      />
      <ol v-else class="divide-y divide-border">
        <li
          v-for="(item, index) in pendingItems"
          :key="item.id"
          class="flex items-center gap-3 px-3 py-2"
          data-testid="pending-item"
        >
          <span class="w-6 shrink-0 text-right font-mono text-xs text-muted-foreground" aria-hidden="true">{{ index + 1 }}</span>
          <span class="min-w-0 flex-1 truncate">{{ item.label }}</span>
          <span class="shrink-0 text-xs text-muted-foreground touch:text-sm">{{ formatTime(item.queuedAt) }}</span>
        </li>
      </ol>
      <p v-if="pendingItems.length > 0" class="border-t border-border px-3 py-2 text-xs text-muted-foreground touch:text-sm">
        Sent in this order as soon as the server can be reached. A change that waits behind one needing attention is sent once that is resolved.
      </p>
    </AppPanel>

    <!-- Offline download -->
    <AppPanel title="Download for offline use">
      <div class="flex flex-col gap-3 text-sm">
        <p class="text-muted-foreground">
          Saves the lists this clinic works from (patients, villages, medication…) on this device, so they can be opened without a connection. It also happens by itself after signing in.
        </p>
        <div v-if="prefetch.running" class="flex flex-col gap-1" role="status">
          <div class="h-2 w-full overflow-hidden border border-border bg-muted" aria-hidden="true">
            <div class="h-full bg-primary transition-[width]" :style="{ width: `${prefetchPercent}%` }" />
          </div>
          <span class="text-xs text-muted-foreground touch:text-sm">Downloading… {{ prefetch.done }} of {{ prefetch.total }}</span>
        </div>
        <p v-else class="text-xs text-muted-foreground touch:text-sm">
          Last downloaded: {{ prefetch.lastRunAt ? formatTime(new Date(prefetch.lastRunAt)) : 'Not yet' }}
          <span v-if="prefetch.failed.length" class="text-warning"> · Not downloaded: {{ prefetch.failed.join(', ') }}</span>
        </p>
        <div>
          <Button size="sm" variant="outline" :disabled="prefetch.running || isOffline" @click="onDownload">
            <PhCloudArrowDown aria-hidden="true" /> Download for offline use
          </Button>
        </div>
      </div>
    </AppPanel>

    <!-- Explanation -->
    <AppPanel title="Working offline">
      <ul class="list-disc space-y-1 pl-5 text-sm text-muted-foreground">
        <li>Anything this device has already opened or downloaded can be read without a connection.</li>
        <li>Saving patient records, visits and station forms works offline. The change shows straight away and is sent when the server can be reached.</li>
        <li>If someone else changed the same record in the meantime, the change is held here so you can decide what to keep.</li>
        <li>Face search, photos and files, PDFs, the audit log, staff accounts and notifications need a connection.</li>
        <li>Signing out deletes changes that have not been sent yet, so sync first.</li>
      </ul>
    </AppPanel>

    <AppDialog v-if="discarding" title="Discard this change?" size="sm" @close="discarding = null">
      <p class="font-semibold">
        {{ discarding.label }}
      </p>
      <p class="mt-2">
        The change is deleted from this device and will never reach the server. This cannot be undone.
      </p>
      <template #footer>
        <Button variant="outline" @click="discarding = null">
          Keep it
        </Button>
        <Button variant="destructive" :disabled="busyId === discarding.id" @click="confirmDiscard">
          <PhTrash aria-hidden="true" /> Discard change
        </Button>
      </template>
    </AppDialog>
  </div>
</template>

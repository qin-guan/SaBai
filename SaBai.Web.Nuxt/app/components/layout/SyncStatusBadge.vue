<script setup lang="ts">
import { PhArrowsClockwise, PhCloudCheck, PhCloudSlash, PhWarning } from '@phosphor-icons/vue'

/** Header badge: offline / N pending / needs attention / all synced. Opens the sync page. */
const { isOffline, replaying, pendingCount, attentionCount, statusLabel } = useSyncState()

const tone = computed(() => {
  if (attentionCount.value > 0) return 'attention'
  if (isOffline.value) return 'offline'
  if (replaying.value || pendingCount.value > 0) return 'pending'
  return 'synced'
})

const toneClass = computed(() => ({
  attention: 'bg-amber-400 text-slate-950 border-amber-300',
  offline: 'bg-amber-500/20 text-amber-100 border-amber-400/40',
  pending: 'bg-sky-500/20 text-sky-100 border-sky-400/40',
  synced: 'bg-emerald-500/20 text-emerald-100 border-emerald-400/40',
})[tone.value])
</script>

<template>
  <NuxtLink
    :to="paths.settingsSync()"
    class="inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-medium transition hover:opacity-90"
    :class="toneClass"
    :title="`Sync: ${statusLabel}`"
    :aria-label="`Sync: ${statusLabel}. Open sync page.`"
    data-testid="sync-status"
  >
    <PhWarning v-if="tone === 'attention'" class="size-3.5 shrink-0" aria-hidden="true" />
    <PhCloudSlash v-else-if="tone === 'offline'" class="size-3.5 shrink-0" aria-hidden="true" />
    <PhArrowsClockwise
      v-else-if="tone === 'pending'"
      class="size-3.5 shrink-0"
      :class="replaying && 'animate-spin motion-reduce:animate-none'"
      aria-hidden="true"
    />
    <PhCloudCheck v-else class="size-3.5 shrink-0" aria-hidden="true" />
    <span class="hidden sm:inline">{{ statusLabel }}</span>
    <span v-if="tone !== 'synced'" class="sm:hidden">{{ attentionCount || pendingCount || '' }}</span>
  </NuxtLink>
</template>

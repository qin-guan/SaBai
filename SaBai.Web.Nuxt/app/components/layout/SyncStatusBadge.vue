<script setup lang="ts">
import { PhArrowsClockwise, PhCloudCheck, PhCloudSlash, PhWarning } from '@phosphor-icons/vue'

const { isOnline, uploading, downloading, connecting, connected, statusLabel, attentionCount } = useSyncState()

const toneClass = computed(() => {
  if (!isOnline.value)
    return 'bg-amber-500/20 text-amber-100 border-amber-400/40'
  if (uploading.value || downloading.value || connecting.value)
    return 'bg-sky-500/20 text-sky-100 border-sky-400/40'
  if (connected.value)
    return 'bg-emerald-500/20 text-emerald-100 border-emerald-400/40'
  return 'bg-white/10 text-chrome-foreground border-white/20'
})
</script>

<template>
  <NuxtLink
    :to="paths.settingsSync()"
    class="inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-medium transition hover:opacity-90"
    :class="toneClass"
    :title="`Sync status: ${statusLabel}`"
  >
    <PhCloudSlash v-if="!isOnline" class="size-3.5 shrink-0" aria-hidden="true" />
    <PhArrowsClockwise
      v-else-if="uploading || downloading || connecting"
      class="size-3.5 shrink-0 animate-spin"
      aria-hidden="true"
    />
    <PhCloudCheck v-else class="size-3.5 shrink-0" aria-hidden="true" />
    <span class="hidden sm:inline">{{ statusLabel }}</span>
    <span
      v-if="attentionCount > 0"
      class="ml-0.5 inline-flex items-center gap-0.5 rounded-full bg-amber-400 px-1.5 py-0.2 text-[11px] font-bold text-slate-950"
      :title="`${attentionCount} sync item(s) need attention`"
    >
      <PhWarning class="size-3" aria-hidden="true" />
      {{ attentionCount }}
    </span>
  </NuxtLink>
</template>

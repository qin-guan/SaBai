<script setup lang="ts">
import { Button } from '@/components/ui/button'

const { $pwa } = useNuxtApp()
const dismissed = ref(false)
const showBanner = computed(() => Boolean($pwa?.needRefresh) && !dismissed.value)

async function applyUpdate() {
  await $pwa?.updateServiceWorker?.(true)
}
</script>

<template>
  <div
    v-if="showBanner"
    role="status"
    class="flex flex-wrap items-center justify-between gap-2 border-b bg-sky-50 px-3 py-2 text-xs text-sky-950"
  >
    <span>A new version of Sa'Bai is ready. Reload when you're between patients to update.</span>
    <div class="flex items-center gap-2">
      <Button size="xs" variant="outline" @click="dismissed = true">
        Later
      </Button>
      <Button size="xs" @click="applyUpdate">
        Reload to update
      </Button>
    </div>
  </div>
</template>

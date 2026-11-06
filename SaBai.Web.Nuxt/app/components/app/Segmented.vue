<script setup lang="ts">
import type { RouteLocationRaw } from 'vue-router'

/**
 * A compact segmented switch whose options are links (the choice lives in the
 * URL, so it survives reloads and can be bookmarked per station).
 */
const props = defineProps<{
  label: string
  value: string
  options: { value: string, label: string, to: RouteLocationRaw }[]
}>()
</script>

<template>
  <div class="flex border border-band-border bg-card" role="group" :aria-label="props.label">
    <NuxtLink
      v-for="opt in props.options"
      :key="opt.value"
      :to="opt.to"
      class="flex h-7 items-center px-2.5 text-xs font-semibold whitespace-nowrap touch:h-11 touch:px-3 touch:text-sm"
      :class="props.value === opt.value ? 'bg-chrome text-white' : 'text-chrome hover:bg-accent'"
      :aria-current="props.value === opt.value ? 'true' : undefined"
    >
      {{ opt.label }}
    </NuxtLink>
  </div>
</template>

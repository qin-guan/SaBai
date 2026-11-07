<script setup lang="ts">
/** Patient display code (e.g. "PC0012") with the village colour swatch. */
const props = defineProps<{
  villageCode: string | null
  villageColorHex: string | null
  id: number
}>()
</script>

<template>
  <span class="inline-flex max-w-full min-w-0 items-center gap-1.5 font-mono tabular-nums">
    <span
      v-if="props.villageColorHex"
      class="size-2.5 shrink-0"
      :style="{ backgroundColor: props.villageColorHex }"
      :title="props.villageCode ?? undefined"
      aria-hidden="true"
    />
    <span class="truncate">{{ formatPatientCode(props.villageCode, props.id) }}</span>
  </span>
</template>

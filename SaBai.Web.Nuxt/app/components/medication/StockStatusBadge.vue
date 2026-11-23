<script setup lang="ts">
/** A batch's status: active in green, anything else (discarded, donated) quiet. */
const props = defineProps<{ status: StockStatus }>()
</script>

<template>
  <span
    class="inline-flex w-fit shrink-0 items-center justify-self-start border px-1.5 py-0.5 text-[0.6875rem] font-semibold"
    :class="props.status === 'active' ? 'border-success/40 bg-success-subtle text-success' : 'border-border bg-muted text-muted-foreground'"
  >
    {{ stockStatusLabel(props.status) }}
  </span>
</template>

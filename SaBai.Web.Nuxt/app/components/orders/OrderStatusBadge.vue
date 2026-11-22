<script setup lang="ts">
import type { OrderStatusKey } from '~/offline/mutations/medication'

/** Pending / Dispensed / Cancelled, in the app's warning / success / destructive colours. */
defineProps<{ status: OrderStatusKey }>()
</script>

<template>
  <span class="inline-flex w-fit shrink-0 items-center justify-self-start border px-1.5 py-0.5 text-[0.6875rem] font-semibold" :class="ORDER_STATUS_CLASSES[status]">
    {{ ORDER_STATUS_LABELS[status] }}
  </span>
</template>

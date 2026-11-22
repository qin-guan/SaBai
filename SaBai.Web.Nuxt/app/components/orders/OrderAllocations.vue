<script setup lang="ts">
import type { OrderAllocation } from '~/utils/medicationCache'

/**
 * The batches an order holds (earliest expiry first, as the server allocates
 * them): what the pharmacist picks from. A pending order without any has not
 * reached the server yet, which chooses the batches.
 */
const props = defineProps<{
  allocations: readonly OrderAllocation[]
  unit?: string
  /** Pending orders say why the list is empty; others show nothing. */
  pending?: boolean
}>()

function formatExpiry(expiry: string) {
  const date = new Date(expiry)
  return Number.isNaN(date.getTime()) ? expiry : date.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}
</script>

<template>
  <ul v-if="props.allocations.length > 0" class="flex flex-col gap-0.5" aria-label="Pick from">
    <li
      v-for="a in props.allocations"
      :key="a.stockId"
      class="grid grid-cols-[1fr_auto] items-baseline gap-x-2 border-l-2 border-band-border pl-1.5 text-xs"
    >
      <span class="min-w-0 break-words">
        <span class="font-semibold">{{ a.brandName }}</span>
        <span class="text-muted-foreground"> · {{ a.location }} · exp {{ formatExpiry(a.expiry) }}</span>
      </span>
      <span class="tabular-nums font-semibold">× {{ a.quantity }}{{ formatQuantityUnit(props.unit) }}</span>
    </li>
  </ul>
  <p v-else-if="props.pending" class="text-xs italic text-muted-foreground">
    Batches are chosen when the order reaches the server.
  </p>
</template>

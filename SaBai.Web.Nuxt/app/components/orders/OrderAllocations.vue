<script setup lang="ts">
import type { OrderAllocation } from '~/utils/medicationCache'

/**
 * The batches an order holds: what the pharmacist picks from (brand, location,
 * expiry and how many, with the unit). They are the system's choice (earliest
 * expiry first) unless the pharmacist chose others. A pending order without
 * any has not reached the server yet, which chooses the batches.
 */
const props = defineProps<{
  allocations: readonly OrderAllocation[]
  unit?: string
  /** Pending orders say why the list is empty; others show nothing. */
  pending?: boolean
  label?: string
}>()
</script>

<template>
  <ul v-if="props.allocations.length > 0" class="flex flex-col gap-1" :aria-label="props.label ?? 'Dispense from'">
    <li
      v-for="a in props.allocations"
      :key="a.stockId"
      class="grid grid-cols-[minmax(0,1fr)_auto] items-baseline gap-x-3 border-l-2 border-band-border pl-2 text-xs touch:text-[0.8125rem]"
      data-allocation
    >
      <span class="min-w-0 break-words whitespace-normal">
        <span class="font-semibold">{{ a.brandName }}</span>
        <span class="text-muted-foreground"> · {{ a.location }} · exp {{ formatExpiryDate(a.expiry) }}</span>
      </span>
      <span class="font-semibold whitespace-nowrap tabular-nums">{{ formatQuantity(a.quantity, props.unit) }}</span>
    </li>
  </ul>
  <p v-else-if="props.pending" class="text-xs whitespace-normal text-muted-foreground italic touch:text-[0.8125rem]">
    Batches are chosen when the order reaches the server.
  </p>
</template>

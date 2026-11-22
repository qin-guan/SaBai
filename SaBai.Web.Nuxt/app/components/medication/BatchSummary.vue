<script setup lang="ts">
/**
 * The batch a dialog is about: ingredient and brand, where it is, its expiry
 * and its three quantities, each with the unit.
 */
const props = defineProps<{ stock: MedicationStockRow }>()

const unit = computed(() => props.stock.unitOfMeasurement)
const expiry = computed(() => batchExpiryFlag(props.stock))
</script>

<template>
  <section aria-label="Batch" class="border border-border">
    <div class="flex flex-wrap items-baseline gap-x-2 gap-y-0.5 border-b border-band-border bg-band px-3 py-1.5">
      <p class="font-bold text-chrome">
        {{ stock.ingredientName }}
      </p>
      <p class="text-xs text-muted-foreground touch:text-sm">
        {{ stock.brandName }} · {{ stock.location }} ·
        <span :class="expiry === 'expired' ? 'font-semibold text-destructive' : expiry === 'soon' ? 'font-semibold text-warning' : ''">
          {{ expiry === 'expired' ? 'expired' : 'expires' }} {{ formatExpiryDate(stock.expiry) }}
        </span>
        <template v-if="stock.stockStatus !== 'active'">
          · {{ stockStatusLabel(stock.stockStatus) }}
        </template>
      </p>
    </div>
    <dl class="grid grid-cols-3 divide-x divide-border text-center">
      <div class="px-2 py-1.5">
        <dt class="text-xs text-muted-foreground touch:text-[0.8125rem]">
          On hand
        </dt>
        <dd class="font-semibold tabular-nums">
          {{ formatQuantity(stock.quantity, unit) }}
        </dd>
      </div>
      <div class="px-2 py-1.5">
        <dt class="text-xs text-muted-foreground touch:text-[0.8125rem]">
          Reserved
        </dt>
        <dd class="font-semibold tabular-nums">
          {{ formatQuantity(stock.reservedQuantity, unit) }}
        </dd>
      </div>
      <div class="px-2 py-1.5">
        <dt class="text-xs text-muted-foreground touch:text-[0.8125rem]">
          Available
        </dt>
        <dd class="font-bold tabular-nums">
          {{ formatQuantity(stock.availableQuantity, unit) }}
        </dd>
      </div>
    </dl>
  </section>
</template>

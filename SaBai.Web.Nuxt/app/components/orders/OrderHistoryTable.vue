<script setup lang="ts">
import { PhArrowsLeftRight, PhCheck, PhPencilSimple, PhTrash, PhX } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import type { MedicationOrder } from '~/offline/mutations/medication'

/**
 * The order log: dispensed and cancelled orders (read-only; a cancelled one
 * can be deleted by the doctor roles) and, in "All orders", pending ones with
 * the same actions as in the queue.
 */
const props = defineProps<{
  orders: readonly MedicationOrder[]
  /** The doctor roles may delete a cancelled order. */
  canDelete: boolean
  dispensing: ReadonlySet<string>
  /** Patient names by order id, for the pending orders the queue knows. */
  patientNames?: ReadonlyMap<string, string>
  /** Heading of the batches column ("Dispensed from" when only dispensed orders are listed). */
  batchesLabel?: string
}>()
const emit = defineEmits<{
  dispense: [order: MedicationOrder]
  change: [order: MedicationOrder]
  cancel: [order: MedicationOrder]
  changeBatch: [order: MedicationOrder]
  delete: [order: MedicationOrder]
}>()

const hasActions = computed(() => props.orders.some(o => o.status === 'PENDING' || (o.status === 'CANCELLED' && props.canDelete)))
</script>

<template>
  <Table class="grid-table stack-table">
    <TableHeader>
      <TableRow>
        <TableHead>Medication</TableHead>
        <TableHead class="text-right">
          Quantity
        </TableHead>
        <TableHead>Status</TableHead>
        <TableHead>{{ props.batchesLabel ?? 'Batches' }}</TableHead>
        <TableHead>Instructions</TableHead>
        <TableHead>Ordered</TableHead>
        <TableHead v-if="hasActions" class="text-right">
          <span class="sr-only">Actions</span>
        </TableHead>
      </TableRow>
    </TableHeader>
    <TableBody>
      <TableRow v-for="order in props.orders" :key="order.id" :data-order-id="order.id">
        <TableCell data-stack="title" class="font-semibold whitespace-normal">
          {{ order.activeIngredientName }}
          <p v-if="props.patientNames?.get(order.id)" class="text-xs font-normal text-muted-foreground touch:text-[0.8125rem]">
            for {{ props.patientNames.get(order.id) }}
          </p>
        </TableCell>
        <TableCell data-label="Quantity" class="text-right font-semibold whitespace-nowrap tabular-nums">
          {{ formatQuantity(order.quantity, order.unitOfMeasurement) }}
        </TableCell>
        <TableCell data-label="Status" class="whitespace-nowrap">
          <OrderStatusBadge :status="order.status" />
          <p v-if="order.status === 'DISPENSED' && order.dispensedAt" class="mt-0.5 text-xs text-muted-foreground tabular-nums touch:text-[0.8125rem]">
            <time :datetime="order.dispensedAt">{{ formatLogTime(order.dispensedAt) }}</time>
          </p>
        </TableCell>
        <TableCell :data-label="order.status === 'DISPENSED' ? 'Dispensed from' : 'Batches'" class="min-w-56 whitespace-normal" :class="order.allocations.length === 0 && order.status !== 'PENDING' && 'max-md:hidden!'">
          <OrderAllocations
            :allocations="order.allocations"
            :unit="order.unitOfMeasurement"
            :pending="order.status === 'PENDING'"
            :label="order.status === 'DISPENSED' ? 'Dispensed from' : 'Dispense from'"
          />
          <span v-if="order.allocations.length === 0 && order.status === 'CANCELLED'" class="text-xs text-muted-foreground max-md:hidden">Released</span>
        </TableCell>
        <TableCell data-label="Instructions" class="min-w-44 whitespace-normal">
          <p>{{ order.dosageInstructions }}</p>
          <p v-if="order.remarks" class="text-xs text-muted-foreground touch:text-[0.8125rem]">
            <span class="font-semibold">Remarks:</span> {{ order.remarks }}
          </p>
        </TableCell>
        <TableCell data-label="Ordered" class="text-xs whitespace-nowrap text-muted-foreground tabular-nums touch:text-[0.8125rem]">
          <time :datetime="order.createdAt">{{ formatLogTime(order.createdAt) }}</time>
        </TableCell>
        <TableCell v-if="hasActions" data-stack="actions" class="text-right" :class="order.status === 'DISPENSED' || (order.status === 'CANCELLED' && !props.canDelete) ? 'max-md:hidden!' : ''">
          <div class="flex flex-wrap items-center justify-end gap-1">
            <template v-if="order.status === 'PENDING'">
              <Button
                v-if="order.allocations.length > 0"
                variant="ghost"
                size="sm"
                :aria-label="`Change batch for ${order.activeIngredientName}`"
                @click="emit('changeBatch', order)"
              >
                <PhArrowsLeftRight aria-hidden="true" /> Batch
              </Button>
              <Button variant="ghost" size="sm" :aria-label="`Change order ${order.activeIngredientName}`" @click="emit('change', order)">
                <PhPencilSimple aria-hidden="true" /> Change
              </Button>
              <Button
                variant="ghost"
                size="sm"
                class="text-destructive hover:text-destructive"
                :aria-label="`Cancel order ${order.activeIngredientName}`"
                @click="emit('cancel', order)"
              >
                <PhX aria-hidden="true" /> Cancel
              </Button>
              <Button
                variant="success"
                size="sm"
                :disabled="props.dispensing.has(order.id)"
                :aria-label="`Dispense ${formatQuantity(order.quantity, order.unitOfMeasurement)} of ${order.activeIngredientName}`"
                @click="emit('dispense', order)"
              >
                <PhCheck aria-hidden="true" /> Dispense
              </Button>
            </template>
            <Button
              v-else-if="order.status === 'CANCELLED' && props.canDelete"
              variant="ghost"
              size="sm"
              class="text-destructive hover:text-destructive"
              :aria-label="`Delete cancelled order ${order.activeIngredientName}`"
              @click="emit('delete', order)"
            >
              <PhTrash aria-hidden="true" /> Delete
            </Button>
          </div>
        </TableCell>
      </TableRow>
    </TableBody>
  </Table>
</template>

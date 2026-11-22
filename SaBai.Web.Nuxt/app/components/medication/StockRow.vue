<script setup lang="ts">
import { PhArrowsSplit, PhCaretDown, PhClockCounterClockwise, PhPencilSimple, PhPlusMinus } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { TableCell, TableRow } from '@/components/ui/table'

/**
 * One batch in the stock list: where it is, what is on hand / reserved /
 * available (always with the unit), its expiry (flagged when past or close)
 * and status. "Log" opens what was dispensed from it; "Manage" holds edit,
 * adjust and split.
 */
const props = defineProps<{
  item: MedicationStockRow
  /** The row above is the same brand: its name is shown quietly. */
  repeatedBrand?: boolean
}>()
const emit = defineEmits<{
  log: [item: MedicationStockRow]
  edit: [item: MedicationStockRow]
  adjust: [item: MedicationStockRow]
  split: [item: MedicationStockRow]
}>()

const unit = computed(() => props.item.unitOfMeasurement)
const canSplit = computed(() => props.item.availableQuantity > 0)
const expiry = computed(() => batchExpiryFlag(props.item))
const expiryText = computed(() => expiryNote(props.item.expiry))
const inactive = computed(() => props.item.stockStatus !== 'active')
const label = computed(() => `${props.item.brandName} at ${props.item.location}`)
</script>

<template>
  <TableRow :data-stock-id="item.id" :class="inactive && 'text-muted-foreground'">
    <TableCell data-stack="title" class="min-w-32">
      <span :class="props.repeatedBrand ? 'md:font-normal md:text-muted-foreground' : 'font-semibold'">{{ item.brandName }}</span>
      <span class="font-normal text-muted-foreground md:hidden"> · {{ item.location }}</span>
    </TableCell>
    <TableCell data-label="Location" class="min-w-28 max-md:hidden!">
      {{ item.location }}
    </TableCell>
    <TableCell data-label="On hand" class="text-right whitespace-nowrap tabular-nums">
      {{ formatQuantity(item.quantity, unit) }}
    </TableCell>
    <TableCell data-label="Reserved" class="text-right whitespace-nowrap tabular-nums" :class="item.reservedQuantity === 0 && 'max-md:hidden!'">
      <template v-if="item.reservedQuantity > 0">
        {{ formatQuantity(item.reservedQuantity, unit) }}
      </template>
      <span v-else class="text-muted-foreground">
        <span aria-hidden="true">–</span><span class="sr-only">None reserved</span>
      </span>
    </TableCell>
    <TableCell data-label="Available" class="text-right font-semibold whitespace-nowrap tabular-nums">
      {{ formatQuantity(item.availableQuantity, unit) }}
    </TableCell>
    <TableCell data-label="Expiry" class="whitespace-nowrap tabular-nums">
      <span class="flex flex-wrap items-center gap-x-2 gap-y-0.5">
        <span>{{ formatExpiryDate(item.expiry) }}</span>
        <span
          v-if="expiry === 'expired'"
          class="inline-flex items-center border border-destructive/40 bg-destructive-subtle px-1.5 py-0.5 text-[0.6875rem] font-semibold text-destructive"
          :title="expiryText"
        >Expired</span>
        <span
          v-else-if="expiry === 'soon'"
          class="inline-flex items-center border border-warning-border bg-warning-subtle px-1.5 py-0.5 text-[0.6875rem] font-semibold text-warning"
        >{{ expiryText.replace('Expires ', '') }}</span>
      </span>
    </TableCell>
    <TableCell data-label="Status">
      <StockStatusBadge :status="item.stockStatus" />
    </TableCell>
    <TableCell data-label="Remarks" class="max-w-56 min-w-24 whitespace-normal" :class="!item.remarks && 'max-md:hidden!'">
      <span class="line-clamp-2 text-xs touch:text-sm" :title="item.remarks ?? undefined">{{ item.remarks }}</span>
    </TableCell>
    <TableCell data-stack="actions">
      <div class="flex justify-end gap-1">
        <Button
          size="sm"
          variant="outline"
          class="max-md:flex-1 touch:h-11"
          :aria-label="`Dispensed log for ${label}`"
          data-stock-action="log"
          @click="emit('log', item)"
        >
          <PhClockCounterClockwise aria-hidden="true" /> Log
        </Button>
        <DropdownMenu>
          <DropdownMenuTrigger as-child>
            <Button size="sm" variant="outline" class="max-md:flex-1 touch:h-11" :aria-label="`Manage ${label}`" data-stock-action="manage">
              Manage <PhCaretDown aria-hidden="true" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" class="min-w-44">
            <DropdownMenuItem class="touch:min-h-11 touch:text-sm" data-stock-action="edit" @select="emit('edit', item)">
              <PhPencilSimple aria-hidden="true" /> Edit details
            </DropdownMenuItem>
            <DropdownMenuItem class="touch:min-h-11 touch:text-sm" data-stock-action="adjust" @select="emit('adjust', item)">
              <PhPlusMinus aria-hidden="true" /> Adjust quantity
            </DropdownMenuItem>
            <DropdownMenuItem
              class="touch:min-h-11 touch:text-sm"
              :disabled="!canSplit"
              data-stock-action="split"
              @select="emit('split', item)"
            >
              <PhArrowsSplit aria-hidden="true" />
              <span class="flex flex-col">
                <span>Split batch</span>
                <span v-if="!canSplit" class="text-[0.6875rem] text-muted-foreground">All of it is reserved</span>
              </span>
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </TableCell>
  </TableRow>
</template>

<script setup lang="ts">
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Button } from '@/components/ui/button'

/**
 * A batch's log, in a side sheet over the stock list: what was dispensed from
 * it (to whom, how much, by whom) with the total, or every change (added,
 * split, adjusted, edited, dispensed). Mount with v-if; Esc, the close button
 * or a click outside closes it and focus returns to the row's Log button.
 */
const props = defineProps<{ stock: MedicationStockRow }>()
const emit = defineEmits<{ close: [] }>()

type View = 'dispensed' | 'all'
const view = ref<View>('dispensed')
const VIEWS = [
  { value: 'dispensed', label: 'Dispensed' },
  { value: 'all', label: 'All changes' },
] as const

const unit = computed(() => props.stock.unitOfMeasurement)
const { data, isPending, isError, error, isFetching, refetch } = useStockChanges(() => props.stock.id, () => view.value === 'dispensed')
const events = computed(() => groupStockChanges(data.value ?? []))
const dispensedTotal = computed(() => totalDispensed(events.value))
const expiry = computed(() => batchExpiryFlag(props.stock))
</script>

<template>
  <Sheet :open="true" @update:open="(open) => !open && emit('close')">
    <SheetContent
      side="right"
      class="w-full gap-0 text-sm data-[side=right]:w-full data-[side=right]:sm:max-w-xl"
      data-stock-log
    >
      <SheetHeader class="shrink-0 gap-0 border-b border-band-border bg-band px-4 py-2.5 pr-12 text-left">
        <SheetTitle class="text-sm font-bold text-chrome">
          Batch log · {{ stock.ingredientName }}
        </SheetTitle>
        <SheetDescription class="text-xs touch:text-sm">
          {{ stock.brandName }} · {{ stock.location }} ·
          <span :class="expiry === 'expired' ? 'font-semibold text-destructive' : ''">{{ expiry === 'expired' ? 'expired' : 'expires' }} {{ formatExpiryDate(stock.expiry) }}</span>
          · {{ formatQuantity(stock.quantity, unit) }} on hand
        </SheetDescription>
      </SheetHeader>

      <div class="flex shrink-0 flex-wrap items-center justify-between gap-2 border-b border-border bg-card px-4 py-2">
        <div role="radiogroup" aria-label="Show" class="flex border border-band-border">
          <button
            v-for="v in VIEWS"
            :key="v.value"
            type="button"
            role="radio"
            class="flex h-7 items-center px-2.5 text-xs font-semibold whitespace-nowrap touch:h-11 touch:px-3 touch:text-sm"
            :class="view === v.value ? 'bg-chrome text-white' : 'bg-card text-chrome hover:bg-accent'"
            :aria-checked="view === v.value"
            :data-log-view="v.value"
            @click="view = v.value"
          >
            {{ v.label }}
          </button>
        </div>
        <p v-if="data" class="text-xs tabular-nums touch:text-sm" role="status" data-log-summary>
          <template v-if="view === 'dispensed'">
            <span class="text-muted-foreground">Total dispensed</span>
            <span class="ml-1.5 font-bold">{{ formatQuantity(dispensedTotal, unit) }}</span>
            <span class="text-muted-foreground"> · {{ events.length }} {{ events.length === 1 ? 'time' : 'times' }}</span>
          </template>
          <span v-else class="text-muted-foreground">{{ events.length }} {{ events.length === 1 ? 'change' : 'changes' }}{{ isFetching ? ' · refreshing…' : '' }}</span>
        </p>
      </div>

      <div class="min-h-0 flex-1 overflow-y-auto">
        <StateMessage v-if="isPending" kind="loading" message="Loading the log…" />
        <div v-else-if="isError && !data" class="flex flex-col items-start gap-2 p-3">
          <StateMessage kind="error" class="w-full" :message="describeLoadError(error, 'The log could not be loaded. Check the connection and try again.')" />
          <Button variant="outline" size="sm" @click="refetch()">
            Try again
          </Button>
        </div>
        <div v-else-if="events.length === 0" class="flex flex-col items-center gap-2 px-4 py-10 text-center" role="status">
          <p class="font-semibold">
            {{ view === 'dispensed' ? 'Nothing dispensed from this batch yet' : 'No changes recorded' }}
          </p>
          <p v-if="view === 'dispensed'" class="text-xs text-muted-foreground touch:text-sm">
            Orders dispensed from it will be listed here with the patient and the quantity.
          </p>
          <Button v-if="view === 'dispensed'" variant="outline" size="sm" @click="view = 'all'">
            Show all changes
          </Button>
        </div>

        <!-- Dispensed: one line per order handed out. -->
        <ol v-else-if="view === 'dispensed'" class="divide-y divide-border" aria-label="Dispensed from this batch">
          <li
            v-for="event in events"
            :key="event.id"
            class="grid grid-cols-[minmax(0,1fr)_auto] gap-x-3 gap-y-0.5 px-4 py-2"
            data-log-entry
          >
            <p class="min-w-0">
              <template v-if="event.dispensing">
                <NuxtLink
                  :to="paths.patientDetail(event.dispensing.patientId)"
                  class="font-semibold text-primary underline-offset-2 hover:underline"
                >{{ event.dispensing.patientName }}</NuxtLink>
                <span class="ml-1.5 font-mono text-xs text-muted-foreground tabular-nums touch:text-[0.8125rem]">{{ event.dispensing.patientDisplayId }}</span>
              </template>
              <span v-else class="font-semibold text-muted-foreground">Patient not recorded</span>
            </p>
            <p class="text-right font-bold whitespace-nowrap tabular-nums">
              {{ formatQuantity(dispensedUnits(event), unit) }}
            </p>
            <p class="text-xs text-muted-foreground touch:text-[0.8125rem]">
              <time :datetime="event.createdAt">{{ formatLogTime(event.createdAt) }}</time> · by {{ event.userName || 'unknown' }}
            </p>
            <p
              v-if="event.dispensing && event.dispensing.orderQuantity !== dispensedUnits(event)"
              class="text-right text-xs text-muted-foreground touch:text-[0.8125rem]"
            >
              of an order for {{ formatQuantity(event.dispensing.orderQuantity, unit) }}
            </p>
          </li>
        </ol>

        <!-- All changes: what happened, the change and what was left. -->
        <ol v-else class="divide-y divide-border" aria-label="All changes to this batch">
          <li
            v-for="event in events"
            :key="event.id"
            class="grid grid-cols-[minmax(0,1fr)_auto] gap-x-3 gap-y-0.5 px-4 py-2"
            data-log-entry
          >
            <p class="min-w-0 font-semibold">
              {{ STOCK_CHANGE_LABELS[event.kind] }}
              <template v-if="event.dispensing">
                <span class="font-normal text-muted-foreground">to&nbsp;</span>
                <NuxtLink
                  :to="paths.patientDetail(event.dispensing.patientId)"
                  class="text-primary underline-offset-2 hover:underline"
                >{{ event.dispensing.patientName }}</NuxtLink>
                <span class="ml-1 font-mono text-xs font-normal text-muted-foreground tabular-nums">{{ event.dispensing.patientDisplayId }}</span>
              </template>
            </p>
            <p class="text-right whitespace-nowrap tabular-nums">
              <template v-if="event.quantityChange">
                <span class="font-bold" :class="event.quantityChange < 0 ? 'text-foreground' : 'text-success'">{{ formatQuantityChange(event.quantityChange, unit) }}</span>
              </template>
              <span v-else class="text-xs text-muted-foreground">no quantity change</span>
            </p>
            <div class="min-w-0 text-xs text-muted-foreground touch:text-[0.8125rem]">
              <p v-for="detail in event.details" :key="detail" class="break-words text-foreground">
                {{ detail }}
              </p>
              <p>
                <time :datetime="event.createdAt">{{ formatLogTime(event.createdAt) }}</time> · by {{ event.userName || 'unknown' }}
              </p>
            </div>
            <p v-if="event.quantityAfter !== null" class="text-right text-xs text-muted-foreground tabular-nums touch:text-[0.8125rem]">
              {{ formatQuantity(event.quantityAfter, unit) }} left
            </p>
          </li>
        </ol>
      </div>
    </SheetContent>
  </Sheet>
</template>

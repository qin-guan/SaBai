<script setup lang="ts">
import { PhArrowCounterClockwise, PhCheck, PhFloppyDisk } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'
import { setOrderAllocations } from '~/offline/mutations/medication'
import type { OrderBatchOption, OrderSnapshot } from '~/offline/mutations/medication'
import type { AllocationChoice } from '~/utils/allocationChoice'

/**
 * Chooses the batches a pending order is dispensed from. The server lists the
 * candidates (earliest expiry first) with what this order may take from each.
 * Tapping a batch takes the whole order from it (or all it has, the rest from
 * the next batches); the quantity fields split it by hand. The choice can be
 * saved for later (the order stays pending) or saved and dispensed at once.
 * The server checks it again and its refusal is shown here.
 */
const props = defineProps<{
  order: {
    orderId: string
    medicationName: string
    quantity: number
    unit?: string
  }
  /** The order as listed, for the optimistic stock edits. */
  before: OrderSnapshot
  visitId?: string
  patientName?: string
}>()
const emit = defineEmits<{ close: [] }>()

const { user } = useCurrentUser()
const { data: options, isPending, isError, error, refetch, isFetching } = useOrderAllocationOptions(() => props.order.orderId)
const saveChoice = useOfflineMutation(setOrderAllocations)
const updateOrder = useUpdateOrder()
const busy = computed(() => saveChoice.isPending.value || updateOrder.isPending.value)

const batches = computed<OrderBatchOption[]>(() => [...(options.value?.batches ?? [])])
const unit = computed(() => options.value?.unitOfMeasurement || props.order.unit)
const quantity = computed(() => options.value?.quantity ?? props.order.quantity)

/** Text of each quantity field by batch; the choice is read from it. */
const fields = ref<Record<string, string>>({})
const serverError = ref<string | null>(null)

function show(choice: AllocationChoice) {
  fields.value = Object.fromEntries(batches.value.map(b => [b.stockId, choice[b.stockId] ? String(choice[b.stockId]) : '']))
}

// Start from what the order holds now; again whenever the server's list is reloaded.
watch(options, (value) => {
  if (value)
    show(currentChoice(value.batches))
}, { immediate: true })

const choice = computed<AllocationChoice>(() => {
  const result: AllocationChoice = {}
  for (const batch of batches.value) {
    const raw = fields.value[batch.stockId] ?? ''
    const value = Number(raw.trim())
    if (raw.trim() && Number.isFinite(value) && value !== 0)
      result[batch.stockId] = value
  }
  return result
})
const check = computed(() => validateChoice(batches.value, choice.value, quantity.value, unit.value))
const systemDefault = computed(() => defaultChoice(batches.value, quantity.value))
const isSystemDefault = computed(() => sameChoice(choice.value, systemDefault.value))
const isUnchanged = computed(() => sameChoice(choice.value, currentChoice(batches.value)))

function useBatch(batch: OrderBatchOption) {
  serverError.value = null
  show(preferBatch(batches.value, batch.stockId, quantity.value))
}

function reset() {
  serverError.value = null
  show(systemDefault.value)
}

function overLimit(batch: OrderBatchOption) {
  return (choice.value[batch.stockId] ?? 0) > batch.maxQuantity
}

/** The chosen batches with the details the lists show. */
function chosenAllocations() {
  return check.value.allocations.map((a) => {
    const batch = batches.value.find(b => b.stockId === a.stockId)!
    return {
      stockId: batch.stockId,
      medicationBrandId: batch.medicationBrandId,
      brandName: batch.brandName,
      location: batch.location,
      expiry: batch.expiry,
      quantity: a.quantity,
    }
  })
}

const forPatient = computed(() => (props.patientName ? ` for ${props.patientName}` : ''))

async function save(dispense: boolean) {
  if (busy.value || !options.value)
    return
  serverError.value = null
  if (!check.value.valid) {
    serverError.value = check.value.message
    return
  }
  const allocations = chosenAllocations()
  try {
    if (dispense) {
      const outcome = await updateOrder.mutateAsync({
        id: props.order.orderId,
        changeId: uuidv7(),
        status: 'DISPENSED',
        // Unchanged: the reserved batches are dispensed as they are.
        allocations: isUnchanged.value ? undefined : allocations,
        before: props.before,
        visitId: props.visitId,
        patientName: props.patientName,
        dispensedById: user.value?.id ?? null,
        dispensedAt: new Date().toISOString(),
      })
      notifySaved(outcome, `Dispensed ${formatQuantity(quantity.value, unit.value)} of ${props.order.medicationName}${forPatient.value}.`)
    }
    else if (!isUnchanged.value) {
      const outcome = await saveChoice.mutateAsync({
        id: props.order.orderId,
        changeId: uuidv7(),
        allocations,
        before: props.before,
        patientName: props.patientName,
      })
      notifySaved(outcome, `Batches chosen for ${props.order.medicationName}${forPatient.value}.`)
    }
    emit('close')
  }
  catch (err) {
    serverError.value = getApiErrorMessage(err, dispense ? 'The order could not be dispensed.' : 'The batches could not be saved.')
    // Someone else may have taken stock meanwhile: show the batches as they are now.
    refetch()
  }
}

useSaveShortcut(() => save(false))
</script>

<template>
  <AppDialog
    title="Choose batches"
    :description="`${props.order.medicationName} · ${formatQuantity(quantity, unit)}${forPatient}`"
    size="lg"
    @close="emit('close')"
  >
    <StateMessage v-if="isPending" kind="loading" message="Loading the batches this order can be dispensed from…" />
    <div v-else-if="isError || !options" class="flex flex-col items-start gap-2">
      <StateMessage
        kind="error"
        class="w-full"
        :message="describeLoadError(error, 'The batches could not be loaded. Choosing a batch needs a connection; the order can still be dispensed from the batches already reserved.')"
      />
      <Button variant="outline" size="sm" @click="refetch()">
        Try again
      </Button>
    </div>
    <div v-else class="flex flex-col gap-3" data-change-batch>
      <div class="flex flex-wrap items-center justify-between gap-2 border border-border bg-stripe px-3 py-2">
        <p class="tabular-nums" aria-live="polite" data-chosen-total>
          <span class="text-base font-bold" :class="check.total === quantity ? 'text-success' : 'text-warning'">{{ check.total }}</span>
          of <span class="font-semibold">{{ formatQuantity(quantity, unit) }}</span> chosen
          <span v-if="!check.valid && check.total > 0" class="ml-1 text-xs font-medium text-warning touch:text-sm">· {{ check.message }}</span>
        </p>
        <Button variant="outline" size="sm" :disabled="isSystemDefault" data-reset-default @click="reset">
          <PhArrowCounterClockwise aria-hidden="true" /> Reset to system default
        </Button>
      </div>

      <FormErrorNote v-if="serverError" :message="serverError" />

      <p class="text-xs text-muted-foreground touch:text-sm">
        Tap a batch to take the order from it, or type how many to take from each. Earliest expiry first.
      </p>

      <ul class="flex flex-col gap-1.5" aria-label="Batches">
        <li
          v-for="batch in batches"
          :key="batch.stockId"
          class="grid grid-cols-1 border sm:grid-cols-[minmax(0,1fr)_11rem]"
          :class="[
            (choice[batch.stockId] ?? 0) > 0 ? 'border-primary bg-accent/40 shadow-[inset_3px_0_0_var(--color-primary)]' : 'border-border bg-card',
            batchUnavailableReason(batch) && 'opacity-70',
          ]"
          :data-batch-id="batch.stockId"
          :data-batch-chosen="(choice[batch.stockId] ?? 0) > 0 || undefined"
        >
          <button
            type="button"
            class="group flex min-w-0 flex-col items-start justify-center gap-0.5 px-3 py-2 text-left focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-ring enabled:hover:bg-accent disabled:cursor-not-allowed"
            :disabled="!!batchUnavailableReason(batch) || busy"
            data-use-batch
            @click="useBatch(batch)"
          >
            <span class="flex w-full flex-wrap items-baseline gap-x-2">
              <span class="font-semibold">{{ batch.brandName }}</span>
              <span class="text-muted-foreground">{{ batch.location }}</span>
              <span
                v-if="(systemDefault[batch.stockId] ?? 0) > 0"
                class="border border-border bg-muted px-1.5 text-[0.6875rem] font-semibold text-muted-foreground"
              >System default</span>
              <span
                v-if="!batchUnavailableReason(batch)"
                class="ml-auto text-xs font-semibold text-primary group-hover:underline touch:text-[0.8125rem]"
              >
                {{ coversOrder(batch, quantity) ? `Use for all ${formatQuantity(quantity, unit)}` : `Take all ${formatQuantity(batch.maxQuantity, unit)}` }}
              </span>
            </span>
            <span class="flex flex-wrap items-center gap-x-2 text-xs tabular-nums touch:text-[0.8125rem]">
              <span :class="batch.isExpired ? 'font-semibold text-destructive' : expiryState(batch.expiry) === 'soon' ? 'font-semibold text-warning' : 'text-muted-foreground'">
                {{ batch.isExpired ? 'Expired' : 'Expires' }} {{ formatExpiryDate(batch.expiry) }}
              </span>
              <span class="text-muted-foreground">· {{ formatQuantity(batch.quantity, unit) }} on hand</span>
              <span class="text-muted-foreground">· {{ formatQuantity(batch.maxQuantity, unit) }} free for this order</span>
            </span>
            <span v-if="batchUnavailableReason(batch)" class="text-xs font-semibold text-destructive touch:text-[0.8125rem]">
              {{ batchUnavailableReason(batch) }}
            </span>
            <span v-else-if="batch.remarks" class="text-xs text-muted-foreground italic touch:text-[0.8125rem]">{{ batch.remarks }}</span>
          </button>
          <div class="flex justify-center gap-0.5 border-t border-border px-3 py-2 max-sm:items-center max-sm:gap-3 sm:flex-col sm:border-t-0 sm:border-l">
            <label :for="`take-${batch.stockId}`" class="text-xs font-semibold touch:text-sm">Take</label>
            <div class="max-sm:flex-1">
              <NumericInput
                :id="`take-${batch.stockId}`"
              v-model="fields[batch.stockId]"
              step="1"
              min="0"
              :max="batch.maxQuantity"
              inputmode="numeric"
              placeholder="0"
              :unit="quantityUnit(unit, choice[batch.stockId] ?? null) || undefined"
              :disabled="!!batchUnavailableReason(batch) || busy"
              :aria-label="`${unit ? quantityUnit(unit) : 'Quantity'} to take from ${batch.brandName} at ${batch.location}`"
              :aria-invalid="overLimit(batch) || undefined"
              :aria-describedby="`take-${batch.stockId}-max`"
              class="bg-card"
              data-take-quantity
              />
            </div>
            <span :id="`take-${batch.stockId}-max`" class="text-[0.6875rem] tabular-nums touch:text-xs" :class="overLimit(batch) ? 'font-semibold text-destructive' : 'text-muted-foreground max-sm:sr-only'">
              {{ batchUnavailableReason(batch) ? 'Not available' : `Up to ${formatQuantity(batch.maxQuantity, unit)}` }}
            </span>
          </div>
        </li>
      </ul>
      <p v-if="batches.length === 0" class="text-xs text-muted-foreground touch:text-sm" role="status">
        There is no batch of this medication to choose from.
      </p>
      <p v-if="isFetching" class="text-xs text-muted-foreground touch:text-sm" role="status">
        Refreshing the batches…
      </p>
    </div>

    <template #footer>
      <span class="mr-auto hidden text-xs text-muted-foreground kb:inline touch:text-sm">
        <ShortcutHint keys="Ctrl+S" /> save choice · <ShortcutHint keys="Esc" /> close
      </span>
      <Button variant="outline" @click="emit('close')">
        Cancel
      </Button>
      <Button variant="outline" :disabled="busy || !options || !check.valid" data-save-choice @click="save(false)">
        <PhFloppyDisk aria-hidden="true" /> {{ saveChoice.isPending.value ? 'Saving…' : 'Save choice' }}
      </Button>
      <Button variant="success" :disabled="busy || !options || !check.valid" data-save-dispense @click="save(true)">
        <PhCheck aria-hidden="true" /> {{ updateOrder.isPending.value ? 'Dispensing…' : 'Save and dispense' }}
      </Button>
    </template>
  </AppDialog>
</template>

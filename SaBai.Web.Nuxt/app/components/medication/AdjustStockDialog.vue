<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import { adjustMedicationStock } from '~/offline/mutations/medication'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

/**
 * Corrects a batch's quantity after a stock count: units removed (damaged,
 * lost, expired and thrown out), units added (found, miscounted) or the
 * counted total itself. The batch cannot go below what is reserved for pending
 * orders; the server checks that too and its message is shown here.
 */
const props = defineProps<{ stock: MedicationStockRow }>()
const emit = defineEmits<{ close: [] }>()

type Mode = 'remove' | 'add' | 'set'
const MODES = [
  { value: 'remove', label: 'Remove' },
  { value: 'add', label: 'Add' },
  { value: 'set', label: 'Set counted total' },
] as const

const adjust = useOfflineMutation(adjustMedicationStock)
const mode = ref<Mode>('remove')
const amount = ref('')
const reason = ref('')
const amountError = ref<string | null>(null)
const serverError = ref<string | null>(null)
const formEl = ref<HTMLFormElement>()
const modeLabelId = useId()

const unit = computed(() => props.stock.unitOfMeasurement)
const typed = computed(() => (amount.value.trim() === '' ? null : Number(amount.value)))
/** Units added (+) or removed (−); null until a usable number is typed. */
const change = computed(() => {
  const n = typed.value
  if (n === null || !Number.isInteger(n) || n < 0)
    return null
  if (mode.value === 'remove') return -n
  if (mode.value === 'add') return n
  return n - props.stock.quantity
})
const after = computed(() => (change.value === null ? null : props.stock.quantity + change.value))
const amountLabel = computed(() => ({ remove: 'Quantity to remove', add: 'Quantity to add', set: 'Counted quantity on hand' })[mode.value])
const amountUnit = computed(() => quantityUnit(unit.value, typed.value) || undefined)

function validate(): string | null {
  const n = typed.value
  if (n === null) return 'Enter a quantity'
  if (!Number.isInteger(n) || n < 0) return 'Quantity must be a whole number'
  if (mode.value !== 'set' && n === 0) return 'Quantity must be at least 1'
  if (change.value === 0) return `The batch already has ${formatQuantity(props.stock.quantity, unit.value)}`
  if (after.value! < 0) return `Only ${formatQuantity(props.stock.quantity, unit.value)} are on hand`
  if (after.value! < props.stock.reservedQuantity)
    return `${formatQuantity(props.stock.reservedQuantity, unit.value)} are reserved for pending orders: the batch cannot go below that`
  return null
}

watch([mode, amount], () => {
  if (amountError.value)
    amountError.value = validate()
})

async function save() {
  if (adjust.isPending.value)
    return
  serverError.value = null
  amountError.value = validate()
  if (amountError.value || change.value === null) {
    nextTick(() => focusFirstInvalid(formEl.value))
    return
  }
  const quantityChange = change.value
  try {
    const outcome = await adjust.mutateAsync({
      id: props.stock.id,
      adjustmentId: uuidv7(),
      quantityChange,
      remarks: reason.value.trim() || null,
      brandName: props.stock.brandName,
      location: props.stock.location,
      unit: unit.value,
    })
    emit('close')
    notifySaved(outcome, `${props.stock.brandName} at ${props.stock.location}: ${formatQuantityChange(quantityChange, unit.value)}, now ${formatQuantity(props.stock.quantity + quantityChange, unit.value)}.`)
  }
  catch (err) {
    serverError.value = getApiErrorMessage(err, 'The quantity could not be adjusted.')
  }
}

useSaveShortcut(save)
</script>

<template>
  <AppDialog title="Adjust quantity" description="After a stock count: record units removed, added, or the counted total." size="md" @close="emit('close')">
    <form id="adjust-stock-form" ref="formEl" novalidate class="flex flex-col gap-3" @submit.prevent="save">
      <BatchSummary :stock="stock" />
      <FormErrorNote v-if="serverError" :message="serverError" />
      <div class="flex flex-col gap-1">
        <span :id="modeLabelId" class="text-xs font-semibold touch:text-sm">Adjustment</span>
        <div role="radiogroup" :aria-labelledby="modeLabelId" class="flex border border-band-border sm:w-fit">
          <button
            v-for="m in MODES"
            :key="m.value"
            type="button"
            role="radio"
            class="flex h-8 flex-1 items-center justify-center border-r border-band-border px-3 text-xs font-semibold whitespace-nowrap last:border-r-0 touch:h-11 touch:text-sm"
            :class="mode === m.value ? 'bg-chrome text-white' : 'bg-card text-chrome hover:bg-accent'"
            :aria-checked="mode === m.value"
            :data-adjust-mode="m.value"
            @click="mode = m.value"
          >
            {{ m.label }}
          </button>
        </div>
      </div>
      <div class="grid grid-cols-1 items-start gap-3 sm:grid-cols-[12rem_1fr]">
        <FormField v-slot="{ id, describedby, invalid }" :label="amountLabel" required :error="amountError">
          <NumericInput
            :id="id"
            v-model="amount"
            step="1"
            min="0"
            inputmode="numeric"
            :unit="amountUnit"
            data-adjust-amount
            :aria-describedby="describedby"
            :aria-invalid="invalid || undefined"
          />
        </FormField>
        <div class="flex flex-col gap-1" aria-live="polite">
          <span class="text-xs font-semibold touch:text-sm">On hand after</span>
          <p class="flex min-h-8 flex-wrap items-center gap-x-2 tabular-nums touch:min-h-11" data-adjust-result>
            <template v-if="after !== null && change !== 0">
              <span class="text-muted-foreground">{{ formatQuantity(stock.quantity, unit) }} →</span>
              <span class="font-bold">{{ formatQuantity(after, unit) }}</span>
              <span :class="change! < 0 ? 'text-destructive' : 'text-success'" class="font-semibold">({{ formatQuantityChange(change, unit) }})</span>
            </template>
            <span v-else class="text-muted-foreground">{{ formatQuantity(stock.quantity, unit) }} (no change)</span>
          </p>
        </div>
      </div>
      <FormField v-slot="{ id, describedby }" label="Reason" hint="Shown in the batch's log and saved as its remarks.">
        <Input :id="id" v-model="reason" placeholder="e.g. Stock count: 4 damaged" :aria-describedby="describedby" />
      </FormField>
    </form>
    <template #footer>
      <span class="mr-auto hidden text-xs text-muted-foreground kb:inline touch:text-sm">
        <ShortcutHint keys="Ctrl+S" /> save · <ShortcutHint keys="Esc" /> close
      </span>
      <Button variant="outline" @click="emit('close')">
        Cancel
      </Button>
      <Button type="submit" form="adjust-stock-form" variant="success" :disabled="adjust.isPending.value">
        <PhFloppyDisk aria-hidden="true" /> {{ adjust.isPending.value ? 'Saving…' : 'Save adjustment' }}
      </Button>
    </template>
  </AppDialog>
</template>

<script setup lang="ts">
import { PhCheck, PhPlus, PhTrash } from '@phosphor-icons/vue'
import { splitMedicationStock } from '~/offline/mutations/medication'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'

/**
 * Splits one stock line into 2–10 distinct child lines whose quantities sum to
 * the parent's available units. Units reserved for pending orders stay in the
 * parent with the orders that hold them.
 */
const props = defineProps<{ stock: MedicationStockRow }>()
const emit = defineEmits<{ close: [] }>()

interface SplitDraft { key: number, location: string, stockStatus: string, quantity: string, remarks: string }
let nextKey = 1
const splits = ref<SplitDraft[]>([])
const list = ref<HTMLElement>()

function addSplit() {
  splits.value.push({
    key: nextKey++,
    location: props.stock.location ?? '',
    stockStatus: props.stock.stockStatus,
    // What is still to be given a place, so two splits of a fresh dialog do not both claim everything.
    quantity: remaining.value > 0 ? String(remaining.value) : '',
    remarks: props.stock.remarks ?? '',
  })
  nextTick(() => {
    const inputs = list.value?.querySelectorAll<HTMLInputElement>('[data-split-location]')
    inputs?.[inputs.length - 1]?.focus()
  })
}

const unit = computed(() => props.stock.unitOfMeasurement)
const error = ref<string | null>(null)
const total = computed(() => splits.value.reduce((sum, s) => sum + (Number.parseInt(s.quantity, 10) || 0), 0))
const remaining = computed(() => props.stock.availableQuantity - total.value)
const split = useOfflineMutation(splitMedicationStock)

async function submit() {
  if (split.isPending.value)
    return
  const payload: SplitPayload[] = splits.value.map(s => ({
    location: s.location,
    stockStatus: s.stockStatus as StockStatus,
    quantity: Number.parseInt(s.quantity, 10),
    remarks: s.remarks || undefined,
  }))
  const result = validateSplits(payload, props.stock.availableQuantity, props.stock.reservedQuantity, unit.value)
  error.value = result.success ? null : result.message
  if (!result.success)
    return

  try {
    // Each child lot gets its id here, so a split queued offline can be sent again without duplicating lots.
    const outcome = await split.mutateAsync({
      stockId: props.stock.id,
      parent: { ...props.stock },
      splits: payload.map(p => ({
        id: uuidv7(),
        quantity: p.quantity,
        location: p.location.trim(),
        stockStatus: p.stockStatus,
        remarks: p.remarks?.trim() || null,
      })),
    })
    emit('close')
    notifySaved(outcome, `Split ${formatQuantity(props.stock.availableQuantity, unit.value)} of ${props.stock.brandName} into ${payload.length} batches.`)
  }
  catch (err) {
    error.value = getApiErrorMessage(err, 'The batch could not be split.')
  }
}

useSaveShortcut(submit)

onMounted(() => {
  // A split needs at least two parts: start with them.
  addSplit()
  addSplit()
})
</script>

<template>
  <AppDialog
    title="Split batch"
    description="Move part of a batch to another place or status. The new batches keep the brand and expiry."
    size="lg"
    @close="emit('close')"
  >
    <div class="flex flex-col gap-3">
      <BatchSummary :stock="stock" />
      <p v-if="stock.reservedQuantity > 0" class="border border-warning-border bg-warning-subtle px-2 py-1.5 text-xs text-warning touch:text-sm">
        {{ formatQuantity(stock.reservedQuantity, unit) }} are reserved for pending orders and stay in this batch.
        Split the other {{ formatQuantity(stock.availableQuantity, unit) }}.
      </p>
      <FormErrorNote v-if="error" :message="error" />
      <AppPanel title="New batches">
        <template #description>
          <p aria-live="polite" data-split-total>
            <span class="font-semibold tabular-nums" :class="remaining === 0 ? 'text-success' : 'text-foreground'">{{ total }} of {{ formatQuantity(stock.availableQuantity, unit) }}</span>
            placed<template v-if="remaining > 0">
              · {{ formatQuantity(remaining, unit) }} left
            </template><template v-else-if="remaining < 0">
              · <span class="font-semibold text-destructive">{{ formatQuantity(-remaining, unit) }} too many</span>
            </template>
          </p>
        </template>
        <template #actions>
          <Button size="sm" variant="outline" :disabled="splits.length >= MAX_SPLITS" @click="addSplit">
            <PhPlus aria-hidden="true" /> Add another
          </Button>
        </template>
        <p v-if="splits.length === 0" class="text-xs text-muted-foreground touch:text-sm">
          Add at least two new batches to split into.
        </p>
        <ol v-else ref="list" class="flex flex-col gap-2">
          <li
            v-for="(s, index) in splits"
            :key="s.key"
            class="grid grid-cols-2 items-end gap-2 border border-border bg-stripe p-2 sm:grid-cols-[1.5rem_minmax(0,1fr)_8rem_9rem_minmax(0,1fr)_auto]"
            :aria-label="`New batch ${index + 1}`"
          >
            <span class="col-span-2 text-xs font-bold text-muted-foreground sm:col-span-1 sm:pb-2 touch:text-sm">{{ index + 1 }}</span>
            <FormField v-slot="{ id }" label="Location">
              <Input :id="id" v-model="s.location" data-split-location />
            </FormField>
            <FormField v-slot="{ id }" label="Status">
              <NativeSelect :id="id" v-model="s.stockStatus">
                <NativeSelectOption v-for="o in stockStatusOptions" :key="o.value" :value="o.value">
                  {{ o.label }}
                </NativeSelectOption>
              </NativeSelect>
            </FormField>
            <FormField v-slot="{ id }" label="Quantity">
              <NumericInput
                :id="id"
                v-model="s.quantity"
                step="1"
                min="1"
                inputmode="numeric"
                data-split-quantity
                :unit="quantityUnit(unit, s.quantity.trim() ? Number(s.quantity) : null) || undefined"
              />
            </FormField>
            <FormField v-slot="{ id }" label="Remarks" class="max-sm:col-span-2">
              <Input :id="id" v-model="s.remarks" />
            </FormField>
            <Button variant="outline" class="max-sm:col-span-2" :aria-label="`Remove new batch ${index + 1}`" @click="splits.splice(index, 1)">
              <PhTrash aria-hidden="true" /> <span class="sm:hidden">Remove</span>
            </Button>
          </li>
        </ol>
      </AppPanel>
    </div>

    <template #footer>
      <span class="mr-auto hidden text-xs text-muted-foreground kb:inline touch:text-sm">
        <ShortcutHint keys="Ctrl+S" /> split · <ShortcutHint keys="Esc" /> close
      </span>
      <Button variant="outline" @click="emit('close')">
        Cancel
      </Button>
      <Button variant="success" :disabled="split.isPending.value" data-split-confirm @click="submit">
        <PhCheck aria-hidden="true" /> {{ split.isPending.value ? 'Splitting…' : 'Split batch' }}
      </Button>
    </template>
  </AppDialog>
</template>

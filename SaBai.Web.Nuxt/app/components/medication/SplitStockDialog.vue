<script setup lang="ts">
import { PhCheck, PhPlus, PhTrash } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { splitMedicationStock } from '~/offline/mutations/medication'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'

/** Splits one stock line into 2–10 distinct child lines whose quantities sum to the parent's. */
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
    quantity: String(props.stock.quantity),
    remarks: props.stock.remarks ?? '',
  })
  nextTick(() => {
    const inputs = list.value?.querySelectorAll<HTMLInputElement>('[data-split-location]')
    inputs?.[inputs.length - 1]?.focus()
  })
}

const total = computed(() => splits.value.reduce((sum, s) => sum + (Number.parseInt(s.quantity, 10) || 0), 0))
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
  const result = validateSplits(payload, props.stock.quantity)
  if (!result.success) {
    toast.error(result.message)
    return
  }

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
    notifySaved(outcome, 'Successfully split!')
  }
  catch (err) {
    console.error(err)
    toast.error(getApiErrorMessage(err, 'An error occurred while splitting stock.'))
  }
}

useSaveShortcut(submit)

const parent = computed(() => [
  ['Location', props.stock.location],
  ['Active Ingredient', props.stock.ingredientName],
  ['Brand Name', props.stock.brandName],
  ['Quantity', String(props.stock.quantity)],
  ['Status', props.stock.stockStatus],
  ['Remarks', props.stock.remarks ?? ''],
])
</script>

<template>
  <AppDialog title="Split Stock" size="lg" @close="emit('close')">
    <AppPanel title="Parent stock details" flush class="mb-3">
      <dl class="grid grid-cols-1 sm:grid-cols-3">
        <div v-for="[label, value] in parent" :key="label" class="border-r border-b border-border px-2 py-1.5">
          <dt class="text-xs touch:text-xs font-semibold text-muted-foreground">
            {{ label }}
          </dt>
          <dd class="font-semibold">
            {{ value || '-' }}
          </dd>
        </div>
      </dl>
    </AppPanel>

    <AppPanel title="Child stock details" :description="`Allocated ${total} of ${props.stock.quantity}`">
      <template #actions>
        <Button size="sm" variant="outline" :disabled="splits.length >= MAX_SPLITS" @click="addSplit">
          <PhPlus aria-hidden="true" /> Add Split
        </Button>
      </template>
      <p v-if="splits.length === 0" class="text-xs text-muted-foreground touch:text-sm">
        No splits added, add a split to begin.
      </p>
      <ol v-else ref="list" class="flex flex-col gap-2">
        <li
          v-for="(s, index) in splits"
          :key="s.key"
          class="grid grid-cols-2 items-end gap-2 border border-border bg-stripe p-2 sm:grid-cols-[2rem_1fr_9rem_6rem_1fr_auto]"
          :aria-label="`Split ${index + 1}`"
        >
          <span class="col-span-2 text-xs font-bold text-muted-foreground sm:col-span-1 sm:pb-2 touch:text-sm">#{{ index + 1 }}</span>
          <FormField v-slot="{ id }" label="Location">
            <Input :id="id" v-model="s.location" data-split-location />
          </FormField>
          <FormField v-slot="{ id }" label="Status">
            <NativeSelect :id="id" v-model="s.stockStatus">
              <NativeSelectOption v-for="status in STOCK_STATUSES" :key="status" :value="status">
                {{ status }}
              </NativeSelectOption>
            </NativeSelect>
          </FormField>
          <FormField v-slot="{ id }" label="Qty">
            <NumericInput :id="id" v-model="s.quantity" step="1" min="1" />
          </FormField>
          <FormField v-slot="{ id }" label="Remarks">
            <Input :id="id" v-model="s.remarks" />
          </FormField>
          <Button variant="outline" :aria-label="`Remove split ${index + 1}`" @click="splits.splice(index, 1)">
            <PhTrash aria-hidden="true" />
          </Button>
        </li>
      </ol>
    </AppPanel>

    <template #footer>
      <Button variant="outline" @click="emit('close')">
        Cancel
      </Button>
      <Button variant="success" :disabled="split.isPending.value" @click="submit">
        <PhCheck aria-hidden="true" /> Confirm
      </Button>
    </template>
  </AppDialog>
</template>

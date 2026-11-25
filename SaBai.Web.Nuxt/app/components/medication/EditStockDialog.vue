<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import { updateMedicationStock } from '~/offline/mutations/medication'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'

/**
 * Edits a batch's location, status and remarks (its quantity changes through
 * "Adjust quantity", its brand and expiry are fixed). While units are reserved
 * for pending orders the status stays active.
 */
const props = defineProps<{ stock: MedicationStockRow }>()
const emit = defineEmits<{ close: [] }>()

const update = useOfflineMutation(updateMedicationStock)
const initial = () => ({
  location: props.stock.location,
  stockStatus: props.stock.stockStatus as string,
  remarks: props.stock.remarks ?? '',
})
const form = ref(initial())
const locationError = ref<string | null>(null)
const serverError = ref<string | null>(null)
const formEl = ref<HTMLFormElement>()
const hasReservations = computed(() => props.stock.reservedQuantity > 0)
const isDirty = computed(() => {
  const before = initial()
  return form.value.location.trim() !== before.location
    || form.value.stockStatus !== before.stockStatus
    || form.value.remarks.trim() !== before.remarks
})

async function save() {
  if (update.isPending.value)
    return
  serverError.value = null
  locationError.value = form.value.location.trim() ? null : 'Enter where the batch is kept'
  if (locationError.value) {
    nextTick(() => focusFirstInvalid(formEl.value))
    return
  }
  if (!isDirty.value) {
    emit('close')
    return
  }
  try {
    const outcome = await update.mutateAsync({
      id: props.stock.id,
      location: form.value.location.trim(),
      stockStatus: form.value.stockStatus as StockStatus,
      remarks: form.value.remarks.trim() || null,
      brandName: props.stock.brandName,
    })
    emit('close')
    notifySaved(outcome, `${props.stock.brandName} batch updated.`)
  }
  catch (err) {
    serverError.value = getApiErrorMessage(err, 'The batch could not be updated. Refresh and try again.')
  }
}

useSaveShortcut(save)
</script>

<template>
  <AppDialog title="Edit batch details" size="md" @close="emit('close')">
    <form id="edit-stock-form" ref="formEl" novalidate class="flex flex-col gap-3" @submit.prevent="save">
      <BatchSummary :stock="stock" />
      <FormErrorNote v-if="serverError" :message="serverError" />
      <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <FormField v-slot="{ id, describedby, invalid }" label="Location" required :error="locationError">
          <Input :id="id" v-model="form.location" :aria-describedby="describedby" :aria-invalid="invalid || undefined" />
        </FormField>
        <FormField
          v-slot="{ id, describedby }"
          label="Status"
          :hint="hasReservations ? `Stays active while ${formatQuantity(stock.reservedQuantity, stock.unitOfMeasurement)} are reserved for pending orders.` : undefined"
        >
          <NativeSelect :id="id" v-model="form.stockStatus" :disabled="hasReservations" :aria-describedby="describedby">
            <NativeSelectOption v-for="o in stockStatusOptions" :key="o.value" :value="o.value">
              {{ o.label }}
            </NativeSelectOption>
          </NativeSelect>
        </FormField>
      </div>
      <FormField v-slot="{ id }" label="Remarks">
        <Input :id="id" v-model="form.remarks" />
      </FormField>
    </form>
    <template #footer>
      <span class="mr-auto hidden text-xs text-muted-foreground kb:inline touch:text-sm">
        <ShortcutHint keys="Ctrl+S" /> save · <ShortcutHint keys="Esc" /> close
      </span>
      <Button variant="outline" @click="emit('close')">
        Cancel
      </Button>
      <Button type="submit" form="edit-stock-form" variant="success" :disabled="update.isPending.value">
        <PhFloppyDisk aria-hidden="true" /> {{ update.isPending.value ? 'Saving…' : 'Save' }}
      </Button>
    </template>
  </AppDialog>
</template>

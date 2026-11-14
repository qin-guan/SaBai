<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { adjustMedicationStock } from '~/offline/mutations/medication'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

/**
 * Adds to or removes from one stock line's quantity (a negative number removes).
 * Works offline: the adjustment carries its own id and the quantity it leads
 * to, so sending it again later never applies it twice.
 */
const props = defineProps<{ stock: MedicationStockRow }>()
const emit = defineEmits<{ close: [] }>()

const form = ref({ quantityChange: '', remarks: '' })
const errors = ref<{ quantityChange?: string }>({})
const formEl = ref<HTMLFormElement>()
const adjust = useOfflineMutation(adjustMedicationStock)

const preview = computed(() => {
  const result = validateAdjustment(form.value.quantityChange, props.stock.quantity)
  return result.success ? result.resultingQuantity : null
})

async function submit() {
  if (adjust.isPending.value)
    return
  const result = validateAdjustment(form.value.quantityChange, props.stock.quantity)
  errors.value = { quantityChange: result.success ? undefined : result.message }
  if (!result.success) {
    focusFirstInvalid(formEl.value)
    return
  }

  try {
    // The adjustment id is made here, when the user confirms, and travels with the queued write.
    const outcome = await adjust.mutateAsync({
      adjustmentId: uuidv7(),
      stockId: props.stock.id,
      quantityChange: result.quantityChange,
      resultingQuantity: result.resultingQuantity,
      remarks: form.value.remarks.trim() || null,
      brandName: props.stock.brandName,
      location: props.stock.location,
    })
    emit('close')
    notifySaved(outcome, 'Stock adjusted.')
  }
  catch (err) {
    console.error(err)
    toast.error(getApiErrorMessage(err, 'An error occurred while adjusting stock.'))
  }
}

useSaveShortcut(submit)
</script>

<template>
  <AppDialog title="Adjust Stock" size="sm" @close="emit('close')">
    <p class="mb-3 text-xs touch:text-sm">
      <span class="font-semibold">{{ stock.ingredientName }} ({{ stock.brandName }})</span>
      at {{ stock.location }}: {{ stock.quantity }} in stock.
    </p>
    <form id="adjust-stock-form" ref="formEl" novalidate class="flex flex-col gap-3" @submit.prevent="submit">
      <FormField v-slot="{ id, describedby, invalid }" label="Quantity change" required :error="errors.quantityChange" hint="Use a negative number to remove stock.">
        <NumericInput :id="id" v-model="form.quantityChange" step="1" :aria-describedby="describedby" :aria-invalid="invalid || undefined" />
      </FormField>
      <p v-if="preview !== null" class="text-xs touch:text-sm text-muted-foreground">
        New quantity: <span class="font-semibold tabular-nums text-foreground">{{ preview }}</span>
      </p>
      <FormField v-slot="{ id }" label="Remarks" hint="Replaces the line's remarks. Leave blank to keep them.">
        <Input :id="id" v-model="form.remarks" />
      </FormField>
    </form>
    <template #footer>
      <Button variant="outline" @click="emit('close')">
        Cancel
      </Button>
      <Button type="submit" form="adjust-stock-form" variant="success" :disabled="adjust.isPending.value">
        <PhFloppyDisk aria-hidden="true" /> {{ adjust.isPending.value ? 'Saving...' : 'Save' }}
      </Button>
    </template>
  </AppDialog>
</template>

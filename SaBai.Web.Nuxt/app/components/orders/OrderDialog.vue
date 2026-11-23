<script setup lang="ts">
import { PhFloppyDisk, PhWarningOctagon } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type { CreateOrderContext, MedicationOrder } from '~/offline/mutations/medication'

/**
 * Adds an order to a saved consult (`consultId`) or changes a pending order
 * (`order`): ingredient, quantity, dosage and remarks. A new quantity or
 * ingredient re-reserves stock on the server; when it cannot be covered the
 * server refuses (409) and nothing changes, and its message is shown here.
 */
const props = defineProps<{
  consultId?: string
  order?: MedicationOrder
  /** For a new order: the patient and visit, so it shows in the pharmacy queue at once. */
  context?: CreateOrderContext
  visitId?: string
  patientName?: string
}>()
const emit = defineEmits<{ close: [] }>()

const isEdit = computed(() => !!props.order)
const availability = useIngredientAvailability()
const createOrderMutation = useCreateOrder()
const updateOrderMutation = useUpdateOrder()
const isPending = computed(() => createOrderMutation.isPending.value || updateOrderMutation.isPending.value)

const form = ref({
  ingredientId: props.order?.medicationActiveIngredientId ?? '',
  quantity: props.order ? String(props.order.quantity) : '1',
  dosageInstructions: props.order?.dosageInstructions ?? '',
  remarks: props.order?.remarks ?? '',
})
const errors = ref<Record<string, string | null>>({})
const serverError = ref<string | null>(null)

const options = computed(() => orderableIngredients(availability.data.value ?? [], {
  holding: props.order ? { ingredientId: props.order.medicationActiveIngredientId, quantity: props.order.quantity } : undefined,
}))
const selected = computed(() => options.value.find(o => o.ingredientId === form.value.ingredientId))
const quantityHint = computed(() => selected.value && !selected.value.outOfStock
  ? `Up to ${formatQuantity(selected.value.remaining, selected.value.unit)}`
  : undefined)
/** The unit beside the quantity field, following the number typed ("1 tablet", "2 tablets"). */
const quantityUnitLabel = computed(() => quantityUnit(selected.value?.unit, form.value.quantity.trim() ? Number(form.value.quantity) : null) || undefined)

function validate() {
  const next: Record<string, string | null> = {}
  if (!form.value.ingredientId) next.ingredientId = 'Please select a medication'
  else if (selected.value?.outOfStock) next.ingredientId = `${selected.value.name} is out of stock`
  next.quantity = orderQuantityError(form.value.quantity, selected.value)
  if (!form.value.dosageInstructions.trim()) next.dosageInstructions = 'Dosage instructions are required'
  errors.value = next
  return Object.values(next).every(e => !e)
}

async function save() {
  if (isPending.value)
    return
  serverError.value = null
  if (!validate())
    return
  const quantity = Number(form.value.quantity)
  const dosageInstructions = form.value.dosageInstructions.trim()
  const remarks = form.value.remarks.trim() || null
  const ingredientName = selected.value?.name ?? props.order?.activeIngredientName ?? 'Medication'

  try {
    if (props.order) {
      const order = props.order
      const changes = {
        medicationActiveIngredientId: form.value.ingredientId !== order.medicationActiveIngredientId ? form.value.ingredientId : undefined,
        quantity: quantity !== order.quantity ? quantity : undefined,
        dosageInstructions: dosageInstructions !== order.dosageInstructions ? dosageInstructions : undefined,
        remarks: remarks !== (order.remarks ?? null) ? remarks : undefined,
      }
      if (Object.values(changes).every(v => v === undefined)) {
        serverError.value = 'Nothing has changed.'
        return
      }
      const outcome = await updateOrderMutation.mutateAsync({
        id: order.id,
        changeId: uuidv7(),
        ...changes,
        ingredientName: changes.medicationActiveIngredientId ? ingredientName : undefined,
        unit: changes.medicationActiveIngredientId ? selected.value?.unit : undefined,
        before: orderSnapshot(order),
        visitId: props.visitId,
        patientName: props.patientName,
      })
      notifySaved(outcome, 'Order updated.')
    }
    else if (props.consultId) {
      const outcome = await createOrderMutation.mutateAsync({
        id: uuidv7(),
        consultId: props.consultId,
        medicationActiveIngredientId: form.value.ingredientId,
        ingredientName,
        unit: selected.value?.unit,
        quantity,
        dosageInstructions,
        remarks,
        context: props.context,
      })
      notifySaved(outcome, `Ordered ${ingredientName}: ${formatQuantity(quantity, selected.value?.unit)}.`)
    }
    emit('close')
  }
  catch (error) {
    serverError.value = getApiErrorMessage(error, isEdit.value ? 'Failed to update the order.' : 'Failed to place the order.')
  }
}

useSaveShortcut(save)
</script>

<template>
  <AppDialog
    :title="isEdit ? 'Change order' : 'Add medication order'"
    :description="isEdit ? 'Only a pending order can be changed. A new quantity or medication reserves stock again.' : 'Stock is reserved for the order as soon as it is placed.'"
    size="md"
    @close="emit('close')"
  >
    <form class="flex flex-col gap-3" novalidate @submit.prevent="save">
      <div
        v-if="serverError"
        class="flex items-start gap-1.5 border border-destructive/40 bg-destructive-subtle px-2 py-1.5 text-xs font-medium text-destructive touch:text-sm"
        role="alert"
      >
        <PhWarningOctagon class="mt-px size-3.5 shrink-0" aria-hidden="true" />
        {{ serverError }}
      </div>
      <StateMessage v-if="!availability.data.value && availability.isError.value" kind="error" :message="describeLoadError(availability.error.value, 'Could not load the medication list.')" />
      <div class="grid grid-cols-1 gap-2 sm:grid-cols-[1fr_11rem]">
        <FormField v-slot="{ id, describedby, invalid }" label="Medication (active ingredient)" required :error="errors.ingredientId">
          <IngredientPicker
            :id="id"
            v-model="form.ingredientId"
            :options="options"
            data-order-ingredient
            :aria-describedby="describedby"
            :aria-invalid="invalid || undefined"
          />
        </FormField>
        <FormField v-slot="{ id, describedby, invalid }" label="Quantity" required :error="errors.quantity" :hint="quantityHint">
          <NumericInput
            :id="id"
            v-model="form.quantity"
            step="1"
            min="1"
            :max="selected?.remaining"
            :unit="quantityUnitLabel"
            :aria-describedby="describedby"
            :aria-invalid="invalid || undefined"
          />
        </FormField>
      </div>
      <FormField v-slot="{ id, describedby, invalid }" label="Dosage instructions" required :error="errors.dosageInstructions">
        <Input
          :id="id"
          v-model="form.dosageInstructions"
          placeholder="e.g. 1 tablet twice daily after meals"
          :aria-describedby="describedby"
          :aria-invalid="invalid || undefined"
        />
      </FormField>
      <FormField v-slot="{ id }" label="Order remarks">
        <Input :id="id" v-model="form.remarks" placeholder="Optional notes for the pharmacist..." />
      </FormField>
    </form>
    <template #footer>
      <Button variant="outline" @click="emit('close')">
        Close
      </Button>
      <Button variant="success" :disabled="isPending" @click="save">
        <PhFloppyDisk aria-hidden="true" />
        {{ isPending ? 'Saving…' : isEdit ? 'Save changes' : 'Place order' }}
      </Button>
    </template>
  </AppDialog>
</template>

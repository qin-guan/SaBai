<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { createMedicationStock } from '~/offline/mutations/medication'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'

const emit = defineEmits<{ close: [] }>()

/**
 * Adds a batch: the brand (which gives the active ingredient and so the unit
 * the quantity is counted in), how many, where it is kept and when it expires.
 */
const form = ref({ location: '', quantity: '', expiry: '', medicationBrandId: '', stockStatus: 'active', remarks: '' })
const errors = ref<Record<string, string>>({})
const serverError = ref<string | null>(null)
const formEl = ref<HTMLFormElement>()

const brands = useMedicationBrandsWithIngredient()
const brandOptions = computed(() =>
  (brands.data.value ?? [])
    .map(b => ({ value: b.id, label: `${b.activeIngredientName} — ${b.name}` }))
    .sort((a, b) => a.label.localeCompare(b.label)),
)
// The brand list does not carry the ingredient's id and level; the stock list needs them.
const brandRecords = useMedicationBrands()
const ingredients = useMedicationIngredients()

const create = useOfflineMutation(createMedicationStock)

/** The chosen brand's ingredient: its unit is what the quantity is counted in. */
const ingredient = computed(() => {
  const ingredientId = brandRecords.data.value?.find(b => b.id === form.value.medicationBrandId)?.activeIngredientId
  return ingredients.data.value?.find(i => i.id === ingredientId)
})
const quantityUnitLabel = computed(() =>
  quantityUnit(ingredient.value?.unitOfMeasurement, form.value.quantity.trim() ? Number(form.value.quantity) : null) || undefined)
const expiryHint = computed(() => {
  if (!form.value.expiry)
    return undefined
  const state = expiryState(form.value.expiry)
  return state === 'ok' ? undefined : expiryNote(form.value.expiry) || undefined
})

async function submit() {
  if (create.isPending.value)
    return
  const e: Record<string, string> = {}
  const f = form.value
  if (!f.location.trim())
    e.location = 'Enter where the batch is kept'
  if (f.quantity === '')
    e.quantity = 'Enter the quantity'
  else if (!Number.isInteger(Number(f.quantity)) || Number(f.quantity) < 1)
    e.quantity = 'Quantity must be a whole number of at least 1'
  if (!f.expiry)
    e.expiry = 'Enter the expiry date'
  if (!f.medicationBrandId)
    e.medicationBrandId = 'Choose the brand'
  if (!f.stockStatus)
    e.stockStatus = 'Choose a status'
  errors.value = e
  serverError.value = null
  if (Object.keys(e).length) {
    focusFirstInvalid(formEl.value)
    return
  }

  const brand = brands.data.value?.find(b => b.id === f.medicationBrandId)
  const chosen = ingredient.value
  if (!brand || !chosen) {
    toast.error('The brand list is not available yet. Please try again.')
    return
  }

  try {
    // The id is chosen here so a create queued offline can be referenced right away.
    const outcome = await create.mutateAsync({
      id: uuidv7(),
      medicationBrandId: brand.id,
      brandName: brand.name,
      ingredientId: chosen.id,
      ingredientName: chosen.name,
      unitOfMeasurement: chosen.unitOfMeasurement,
      fallBelow: chosen.fallBelow,
      quantity: Number(f.quantity),
      expiry: f.expiry,
      location: f.location.trim(),
      stockStatus: f.stockStatus as StockStatus,
      remarks: f.remarks.trim() || null,
    })
    emit('close')
    notifySaved(outcome, `Added ${formatQuantity(Number(f.quantity), chosen.unitOfMeasurement)} of ${brand.name} at ${f.location.trim()}.`)
  }
  catch (err) {
    console.error(err)
    serverError.value = getApiErrorMessage(err, 'The batch could not be added.')
  }
}

useSaveShortcut(submit)
</script>

<template>
  <AppDialog title="Add batch" description="A delivery of one brand with one expiry date, kept in one place." size="md" @close="emit('close')">
    <form id="create-stock-form" ref="formEl" novalidate class="grid grid-cols-1 gap-3 sm:grid-cols-2" @submit.prevent="submit">
      <FormErrorNote v-if="serverError" :message="serverError" class="sm:col-span-2" />
      <FormField v-slot="{ id, describedby, invalid }" label="Active ingredient and brand" required :error="errors.medicationBrandId" class="sm:col-span-2">
        <StateMessage v-if="!brands.data.value && brands.isLoading.value" kind="loading" message="Loading brands…" />
        <StateMessage
          v-else-if="!brands.data.value && brands.isError.value"
          kind="error"
          :message="describeLoadError(brands.error.value, 'The brand list could not be loaded. Check the connection and reload.')"
        />
        <NativeSelect v-else :id="id" v-model="form.medicationBrandId" :aria-describedby="describedby" :aria-invalid="invalid || undefined">
          <NativeSelectOption value="">
            Select a brand
          </NativeSelectOption>
          <NativeSelectOption v-for="o in brandOptions" :key="o.value" :value="o.value">
            {{ o.label }}
          </NativeSelectOption>
        </NativeSelect>
      </FormField>
      <FormField
        v-slot="{ id, describedby, invalid }"
        label="Quantity"
        required
        :error="errors.quantity"
        :hint="ingredient ? `Counted in ${quantityUnit(ingredient.unitOfMeasurement)}` : 'Choose the brand to see the unit'"
      >
        <NumericInput
          :id="id"
          v-model="form.quantity"
          step="1"
          min="1"
          inputmode="numeric"
          :unit="quantityUnitLabel"
          :aria-describedby="describedby"
          :aria-invalid="invalid || undefined"
        />
      </FormField>
      <FormField v-slot="{ id, describedby, invalid }" label="Expiry date" required :error="errors.expiry" :hint="expiryHint">
        <Input :id="id" v-model="form.expiry" type="date" :aria-describedby="describedby" :aria-invalid="invalid || undefined" />
      </FormField>
      <FormField v-slot="{ id, describedby, invalid }" label="Location" required :error="errors.location">
        <Input :id="id" v-model="form.location" placeholder="e.g. Shelf A1" :aria-describedby="describedby" :aria-invalid="invalid || undefined" />
      </FormField>
      <FormField v-slot="{ id, describedby, invalid }" label="Status" required :error="errors.stockStatus">
        <NativeSelect :id="id" v-model="form.stockStatus" :aria-describedby="describedby" :aria-invalid="invalid || undefined">
          <NativeSelectOption v-for="o in stockStatusOptions" :key="o.value" :value="o.value">
            {{ o.label }}
          </NativeSelectOption>
        </NativeSelect>
      </FormField>
      <FormField v-slot="{ id }" label="Remarks" class="sm:col-span-2">
        <Input :id="id" v-model="form.remarks" />
      </FormField>
    </form>
    <template #footer>
      <Button variant="outline" @click="emit('close')">
        Cancel
      </Button>
      <Button type="submit" form="create-stock-form" variant="success" :disabled="create.isPending.value">
        <PhFloppyDisk aria-hidden="true" /> {{ create.isPending.value ? 'Saving…' : 'Add batch' }}
      </Button>
    </template>
  </AppDialog>
</template>

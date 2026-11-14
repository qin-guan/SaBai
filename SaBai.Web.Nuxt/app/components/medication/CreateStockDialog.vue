<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { createMedicationStock } from '~/offline/mutations/medication'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'

const emit = defineEmits<{ close: [] }>()

const form = ref({ location: '', quantity: '', expiry: '', medicationBrandId: '', stockStatus: '', remarks: '' })
const errors = ref<Record<string, string>>({})
const formEl = ref<HTMLFormElement>()

const brands = useMedicationBrandsWithIngredient()
const brandOptions = computed(() =>
  (brands.data.value ?? []).map(b => ({ value: b.id, label: `${b.activeIngredientName} (${b.name})` })),
)
// The brand list does not carry the ingredient's id and level; the stock list needs them.
const brandRecords = useMedicationBrands()
const ingredients = useMedicationIngredients()

const create = useOfflineMutation(createMedicationStock)

async function submit() {
  if (create.isPending.value)
    return
  const e: Record<string, string> = {}
  const f = form.value
  if (!f.location.trim())
    e.location = 'Missing Location!'
  if (f.quantity === '')
    e.quantity = 'Missing Quantity!'
  else if (Number(f.quantity) < 1)
    e.quantity = 'Please input only positive values.'
  if (!f.expiry)
    e.expiry = 'Missing Expiry Date!'
  if (!f.medicationBrandId)
    e.medicationBrandId = 'Missing Brand + Active Ingredient!'
  if (!f.stockStatus)
    e.stockStatus = 'Missing Status!'
  errors.value = e
  if (Object.keys(e).length) {
    focusFirstInvalid(formEl.value)
    return
  }

  const brand = brands.data.value?.find(b => b.id === f.medicationBrandId)
  const ingredientId = brandRecords.data.value?.find(b => b.id === f.medicationBrandId)?.activeIngredientId
  const ingredient = ingredients.data.value?.find(i => i.id === ingredientId)
  if (!brand || !ingredientId || !ingredient) {
    toast.error('The brand list is not available yet. Please try again.')
    return
  }

  try {
    // The id is chosen here so a create queued offline can be referenced right away.
    const outcome = await create.mutateAsync({
      id: uuidv7(),
      medicationBrandId: brand.id,
      brandName: brand.name,
      ingredientId,
      ingredientName: ingredient.name,
      fallBelow: ingredient.fallBelow,
      quantity: Number(f.quantity),
      expiry: f.expiry,
      location: f.location.trim(),
      stockStatus: f.stockStatus as StockStatus,
      remarks: f.remarks.trim() || null,
    })
    emit('close')
    notifySaved(outcome, 'Successfully created!')
  }
  catch (err) {
    console.error(err)
    toast.error(getApiErrorMessage(err, 'An error has occurred.'))
  }
}

useSaveShortcut(submit)
</script>

<template>
  <AppDialog title="Add New Stock" size="md" @close="emit('close')">
    <form id="create-stock-form" ref="formEl" novalidate class="grid grid-cols-1 gap-3 sm:grid-cols-2" @submit.prevent="submit">
      <FormField v-slot="{ id, describedby, invalid }" label="Brand + Active Ingredient" required :error="errors.medicationBrandId" class="sm:col-span-2">
        <StateMessage v-if="!brands.data.value && brands.isLoading.value" kind="loading" message="Loading brands with active ingredients..." />
        <StateMessage
          v-else-if="!brands.data.value && brands.isError.value"
          kind="error"
          :message="describeLoadError(brands.error.value, 'An error has occurred while loading the dropdown options for brand + active ingredient. Please refresh the page.')"
        />
        <NativeSelect v-else :id="id" v-model="form.medicationBrandId" :aria-describedby="describedby" :aria-invalid="invalid || undefined">
          <NativeSelectOption value="">
            Select an option
          </NativeSelectOption>
          <NativeSelectOption v-for="o in brandOptions" :key="o.value" :value="o.value">
            {{ o.label }}
          </NativeSelectOption>
        </NativeSelect>
      </FormField>
      <FormField v-slot="{ id, describedby, invalid }" label="Location" required :error="errors.location">
        <Input :id="id" v-model="form.location" :aria-describedby="describedby" :aria-invalid="invalid || undefined" />
      </FormField>
      <FormField v-slot="{ id, describedby, invalid }" label="Quantity" required :error="errors.quantity">
        <NumericInput :id="id" v-model="form.quantity" step="1" min="1" :aria-describedby="describedby" :aria-invalid="invalid || undefined" />
      </FormField>
      <FormField v-slot="{ id, describedby, invalid }" label="Expiry Date" required :error="errors.expiry">
        <Input :id="id" v-model="form.expiry" type="date" :aria-describedby="describedby" :aria-invalid="invalid || undefined" />
      </FormField>
      <FormField v-slot="{ id, describedby, invalid }" label="Status" required :error="errors.stockStatus">
        <NativeSelect :id="id" v-model="form.stockStatus" :aria-describedby="describedby" :aria-invalid="invalid || undefined">
          <NativeSelectOption value="">
            Select an option
          </NativeSelectOption>
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
        <PhFloppyDisk aria-hidden="true" /> {{ create.isPending.value ? 'Saving...' : 'Save' }}
      </Button>
    </template>
  </AppDialog>
</template>

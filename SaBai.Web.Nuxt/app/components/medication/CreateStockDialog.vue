<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
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

const isSaving = ref(false)

async function submit() {
  if (isSaving.value)
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

  isSaving.value = true
  try {
    const db = getPowerSyncInstance()
    const now = new Date().toISOString()
    await db.execute(
      `INSERT INTO medication_stocks (
        id, medication_brand_id, quantity, expiry, location, stock_status, remarks, version, updated_at
      ) VALUES (?, ?, ?, ?, ?, ?, ?, 1, ?)`,
      [
        uuidv7(),
        f.medicationBrandId,
        Number(f.quantity),
        f.expiry,
        f.location.trim(),
        f.stockStatus,
        f.remarks.trim() || null,
        now,
      ],
    )
    await refreshPendingCrudCount()
    toast.success('Successfully created!')
    emit('close')
  }
  catch (err) {
    console.error(err)
    toast.error('An error has occurred.')
  }
  finally {
    isSaving.value = false
  }
}

useSaveShortcut(submit)
</script>

<template>
  <AppDialog title="Add New Stock" size="md" @close="emit('close')">
    <form id="create-stock-form" ref="formEl" novalidate class="grid grid-cols-1 gap-3 sm:grid-cols-2" @submit.prevent="submit">
      <FormField v-slot="{ id, describedby, invalid }" label="Brand + Active Ingredient" required :error="errors.medicationBrandId" class="sm:col-span-2">
        <StateMessage v-if="brands.isLoading.value" kind="loading" message="Loading brands with active ingredients..." />
        <StateMessage
          v-else-if="brands.isError.value"
          kind="error"
          message="An error has occurred while loading the dropdown options for brand + active ingredient. Please refresh the page."
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
      <Button type="submit" form="create-stock-form" variant="success" :disabled="isSaving">
        <PhFloppyDisk aria-hidden="true" /> {{ isSaving ? 'Saving...' : 'Save' }}
      </Button>
    </template>
  </AppDialog>
</template>

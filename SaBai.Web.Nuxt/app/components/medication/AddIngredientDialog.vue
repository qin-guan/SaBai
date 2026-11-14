<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

const emit = defineEmits<{ close: [] }>()
const form = ref({ name: '', unitOfMeasurement: '', fallBelow: '', remarks: '' })
const fallBelowError = ref<string | null>(null)
const formEl = ref<HTMLFormElement>()
const isSaving = ref(false)

async function submit() {
  if (isSaving.value)
    return
  const n = Number(form.value.fallBelow)
  fallBelowError.value = form.value.fallBelow === '' ? 'Missing Fall below!' : n < 1 ? 'Please input only positive values.' : null
  if (fallBelowError.value) {
    focusFirstInvalid(formEl.value)
    return
  }

  isSaving.value = true
  try {
    const db = getPowerSyncInstance()
    await db.execute(
      `INSERT INTO medication_active_ingredients (id, name, unit_of_measurement, fall_below, remarks, version)
       VALUES (?, ?, ?, ?, ?, 1)`,
      [
        uuidv7(),
        form.value.name.trim(),
        form.value.unitOfMeasurement.trim(),
        n,
        form.value.remarks.trim() || null,
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
  <AppDialog title="Add New Active Ingredient" size="sm" @close="emit('close')">
    <form id="add-ingredient-form" ref="formEl" novalidate class="flex flex-col gap-3" @submit.prevent="submit">
      <FormField v-slot="{ id }" label="Name">
        <Input :id="id" v-model="form.name" />
      </FormField>
      <FormField v-slot="{ id }" label="Unit of Measurement">
        <Input :id="id" v-model="form.unitOfMeasurement" />
      </FormField>
      <FormField v-slot="{ id, describedby, invalid }" label="Fall below" required :error="fallBelowError">
        <NumericInput :id="id" v-model="form.fallBelow" step="1" min="1" :aria-describedby="describedby" :aria-invalid="invalid || undefined" />
      </FormField>
      <FormField v-slot="{ id }" label="Remarks">
        <Input :id="id" v-model="form.remarks" />
      </FormField>
    </form>
    <template #footer>
      <Button variant="outline" @click="emit('close')">
        Cancel
      </Button>
      <Button type="submit" form="add-ingredient-form" variant="success" :disabled="isSaving">
        <PhFloppyDisk aria-hidden="true" /> {{ isSaving ? 'Saving...' : 'Save' }}
      </Button>
    </template>
  </AppDialog>
</template>

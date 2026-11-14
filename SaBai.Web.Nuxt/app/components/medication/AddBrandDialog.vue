<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import type { LocalMedicationIngredient } from '~/composables/useMedication'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'

defineProps<{ ingredients: LocalMedicationIngredient[] }>()
const emit = defineEmits<{ close: [] }>()
const form = ref({ name: '', activeIngredientId: '', remarks: '' })
const errors = ref<{ name?: string, activeIngredientId?: string }>({})
const formEl = ref<HTMLFormElement>()
const isSaving = ref(false)

async function submit() {
  if (isSaving.value)
    return
  errors.value = {
    name: form.value.name.trim() ? undefined : 'Missing name!',
    activeIngredientId: form.value.activeIngredientId ? undefined : 'Select an active ingredient.',
  }
  if (errors.value.name || errors.value.activeIngredientId) {
    focusFirstInvalid(formEl.value)
    return
  }

  isSaving.value = true
  try {
    const db = getPowerSyncInstance()
    await db.execute(
      `INSERT INTO medication_brands (id, name, active_ingredient_id, remarks, version)
       VALUES (?, ?, ?, ?, 1)`,
      [
        uuidv7(),
        form.value.name.trim(),
        form.value.activeIngredientId,
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
  <AppDialog title="Add New Brand" size="sm" @close="emit('close')">
    <form id="add-brand-form" ref="formEl" novalidate class="flex flex-col gap-3" @submit.prevent="submit">
      <FormField v-slot="{ id, describedby, invalid }" label="Name" required :error="errors.name">
        <Input :id="id" v-model="form.name" :aria-describedby="describedby" :aria-invalid="invalid || undefined" />
      </FormField>
      <FormField v-slot="{ id, describedby, invalid }" label="Active Ingredient" required :error="errors.activeIngredientId">
        <NativeSelect :id="id" v-model="form.activeIngredientId" :aria-describedby="describedby" :aria-invalid="invalid || undefined">
          <NativeSelectOption value="">
            Select an option
          </NativeSelectOption>
          <NativeSelectOption v-for="i in ingredients" :key="i.id" :value="String(i.id)">
            {{ i.name }} ({{ i.unitOfMeasurement }})
          </NativeSelectOption>
        </NativeSelect>
      </FormField>
      <FormField v-slot="{ id }" label="Remarks">
        <Input :id="id" v-model="form.remarks" />
      </FormField>
    </form>
    <template #footer>
      <Button variant="outline" @click="emit('close')">
        Cancel
      </Button>
      <Button type="submit" form="add-brand-form" variant="success" :disabled="isSaving">
        <PhFloppyDisk aria-hidden="true" /> {{ isSaving ? 'Saving...' : 'Save' }}
      </Button>
    </template>
  </AppDialog>
</template>

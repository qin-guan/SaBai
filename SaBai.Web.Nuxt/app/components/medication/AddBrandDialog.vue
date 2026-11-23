<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import { createMedicationBrand } from '~/offline/mutations/medication'
import type { MedicationIngredient } from '~/offline/mutations/medication'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'

const props = defineProps<{ ingredients: MedicationIngredient[] }>()
const emit = defineEmits<{ close: [] }>()
const form = ref({ name: '', activeIngredientId: '', remarks: '' })
const errors = ref<{ name?: string, activeIngredientId?: string }>({})
const serverError = ref<string | null>(null)
const formEl = ref<HTMLFormElement>()
const create = useOfflineMutation(createMedicationBrand)

async function submit() {
  if (create.isPending.value)
    return
  serverError.value = null
  errors.value = {
    name: form.value.name.trim() ? undefined : 'Enter the brand name',
    activeIngredientId: form.value.activeIngredientId ? undefined : 'Choose the active ingredient',
  }
  if (errors.value.name || errors.value.activeIngredientId) {
    focusFirstInvalid(formEl.value)
    return
  }

  const ingredient = props.ingredients.find(i => i.id === form.value.activeIngredientId)
  if (!ingredient) {
    errors.value = { activeIngredientId: 'Choose the active ingredient' }
    return
  }
  try {
    // The id is chosen here so a create queued offline can be referenced right away.
    const outcome = await create.mutateAsync({
      id: uuidv7(),
      name: form.value.name.trim(),
      activeIngredientId: ingredient.id,
      remarks: form.value.remarks.trim(),
      ingredientName: ingredient.name,
      ingredientFallBelow: ingredient.fallBelow,
    })
    emit('close')
    notifySaved(outcome, `Added brand ${form.value.name.trim()} (${ingredient.name}).`)
  }
  catch (err) {
    serverError.value = getApiErrorMessage(err, 'The brand could not be added.')
  }
}

useSaveShortcut(submit)
</script>

<template>
  <AppDialog title="Add brand" size="sm" @close="emit('close')">
    <form id="add-brand-form" ref="formEl" novalidate class="flex flex-col gap-3" @submit.prevent="submit">
      <FormErrorNote v-if="serverError" :message="serverError" />
      <FormField v-slot="{ id, describedby, invalid }" label="Name" required :error="errors.name">
        <Input :id="id" v-model="form.name" :aria-describedby="describedby" :aria-invalid="invalid || undefined" />
      </FormField>
      <FormField v-slot="{ id, describedby, invalid }" label="Active ingredient" required :error="errors.activeIngredientId">
        <NativeSelect :id="id" v-model="form.activeIngredientId" :aria-describedby="describedby" :aria-invalid="invalid || undefined">
          <NativeSelectOption value="">
            Select an active ingredient
          </NativeSelectOption>
          <NativeSelectOption v-for="i in ingredients" :key="i.id" :value="i.id">
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
      <Button type="submit" form="add-brand-form" variant="success" :disabled="create.isPending.value">
        <PhFloppyDisk aria-hidden="true" /> {{ create.isPending.value ? 'Saving…' : 'Add brand' }}
      </Button>
    </template>
  </AppDialog>
</template>

<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import { saBaiAPIEndpointsMedicationIngredientsCreatePostEndpoint } from '@sabai/sdk/clients'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

const emit = defineEmits<{ close: [] }>()
const form = ref({ name: '', unitOfMeasurement: '', fallBelow: '', remarks: '' })
const fallBelowError = ref<string | null>(null)
const formEl = ref<HTMLFormElement>()

const queryClient = useQueryClient()
const create = useMutation({
  mutationFn: () =>
    saBaiAPIEndpointsMedicationIngredientsCreatePostEndpoint({
      ...apiConfig,
      body: {
        name: form.value.name,
        unitOfMeasurement: form.value.unitOfMeasurement,
        fallBelow: Number(form.value.fallBelow),
        remarks: form.value.remarks,
      },
      throwOnError: true,
    }).unwrap(),
  onSuccess: () => {
    toast.success('Successfully created!')
    queryClient.invalidateQueries({ queryKey: queryKeys.medicationIngredients() })
    emit('close')
  },
  onError: (err) => {
    console.error(err)
    toast.error(getApiErrorMessage(err, 'An error has occurred.'))
  },
})

function submit() {
  if (create.isPending.value)
    return
  const n = Number(form.value.fallBelow)
  fallBelowError.value = form.value.fallBelow === '' ? 'Missing Fall below!' : n < 1 ? 'Please input only positive values.' : null
  if (fallBelowError.value) {
    focusFirstInvalid(formEl.value)
    return
  }
  create.mutate()
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
      <Button type="submit" form="add-ingredient-form" variant="success" :disabled="create.isPending.value">
        <PhFloppyDisk aria-hidden="true" /> {{ create.isPending.value ? 'Saving...' : 'Save' }}
      </Button>
    </template>
  </AppDialog>
</template>

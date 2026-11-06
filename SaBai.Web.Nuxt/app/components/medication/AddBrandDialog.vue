<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import { saBaiAPIEndpointsMedicationBrandsCreatePostEndpoint } from '@sabai/sdk/clients'
import type { SaBaiAPIEndpointsMedicationIngredientsListGetResponse } from '@sabai/sdk/types'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'

defineProps<{ ingredients: SaBaiAPIEndpointsMedicationIngredientsListGetResponse[] }>()
const emit = defineEmits<{ close: [] }>()
const form = ref({ name: '', activeIngredientId: '', remarks: '' })
const errors = ref<{ name?: string, activeIngredientId?: string }>({})
const formEl = ref<HTMLFormElement>()

const queryClient = useQueryClient()
const create = useMutation({
  mutationFn: () =>
    saBaiAPIEndpointsMedicationBrandsCreatePostEndpoint({
      ...apiConfig,
      body: {
        name: form.value.name.trim(),
        activeIngredientId: Number(form.value.activeIngredientId),
        remarks: form.value.remarks,
      },
      throwOnError: true,
    }).unwrap(),
  onSuccess: () => {
    toast.success('Successfully created!')
    // Also covers the brand + ingredient list used by the stock forms.
    queryClient.invalidateQueries({ queryKey: queryKeys.medicationBrands() })
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
  errors.value = {
    name: form.value.name.trim() ? undefined : 'Missing name!',
    activeIngredientId: form.value.activeIngredientId ? undefined : 'Select an active ingredient.',
  }
  if (errors.value.name || errors.value.activeIngredientId) {
    focusFirstInvalid(formEl.value)
    return
  }
  create.mutate()
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
      <Button type="submit" form="add-brand-form" variant="success" :disabled="create.isPending.value">
        <PhFloppyDisk aria-hidden="true" /> {{ create.isPending.value ? 'Saving...' : 'Save' }}
      </Button>
    </template>
  </AppDialog>
</template>

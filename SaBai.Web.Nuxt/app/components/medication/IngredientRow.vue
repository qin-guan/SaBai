<script setup lang="ts">
import { PhFloppyDisk, PhPencilSimple, PhTrash, PhX } from '@phosphor-icons/vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import {
  saBaiAPIEndpointsMedicationIngredientsDeleteDeleteEndpoint,
  saBaiAPIEndpointsMedicationIngredientsUpdatePutEndpoint,
} from '@sabai/sdk/clients'
import type { SaBaiAPIEndpointsMedicationIngredientsListGetResponse } from '@sabai/sdk/types'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { TableCell, TableRow } from '@/components/ui/table'

/** One active ingredient; inline edit (Esc cancels, Enter saves) and delete with confirmation. */
const props = defineProps<{ ingredient: SaBaiAPIEndpointsMedicationIngredientsListGetResponse }>()

const isEditing = ref(false)
const isDeleting = ref(false)
const initial = () => ({
  name: props.ingredient.name,
  unitOfMeasurement: props.ingredient.unitOfMeasurement,
  fallBelow: String(props.ingredient.fallBelow),
  remarks: props.ingredient.remarks ?? '',
})
const form = ref(initial())
const fallBelowError = ref<string | null>(null)
const firstInput = ref<InstanceType<typeof Input>>()

function startEditing() {
  form.value = initial()
  isEditing.value = true
  nextTick(() => (firstInput.value?.$el as HTMLInputElement | undefined)?.focus())
}
function cancel() {
  form.value = initial()
  fallBelowError.value = null
  isEditing.value = false
}

const queryClient = useQueryClient()
const invalidate = () => queryClient.invalidateQueries({ queryKey: queryKeys.medicationIngredients() })

const update = useMutation({
  mutationFn: () =>
    saBaiAPIEndpointsMedicationIngredientsUpdatePutEndpoint({
      ...apiConfig,
      path: { id: props.ingredient.id },
      body: {
        name: form.value.name,
        unitOfMeasurement: form.value.unitOfMeasurement,
        fallBelow: Number(form.value.fallBelow),
        remarks: form.value.remarks,
      },
      throwOnError: true,
    }).unwrap(),
  onSuccess: () => {
    toast.success('Successfully updated!')
    invalidate()
  },
  onError: (err) => {
    toast.error(getApiErrorMessage(err, 'An error has occurred.'))
    console.error(err)
    form.value = initial()
  },
})

const remove = useMutation({
  mutationFn: () =>
    saBaiAPIEndpointsMedicationIngredientsDeleteDeleteEndpoint({
      ...apiConfig,
      path: { id: props.ingredient.id },
      throwOnError: true,
    }).unwrap(),
  onSuccess: () => {
    toast.success('Successfully deleted.')
    isDeleting.value = false
    invalidate()
  },
  onError: (err) => {
    toast.error(getApiErrorMessage(err, 'Error! Check that there are no brands that depend on this active ingredient.'))
    isDeleting.value = false
  },
})

function save() {
  const n = Number(form.value.fallBelow)
  fallBelowError.value = form.value.fallBelow === '' ? 'Missing Fall below!' : n < 1 ? 'Please input only positive values.' : null
  if (fallBelowError.value)
    return
  const i = props.ingredient
  const changed = form.value.name !== i.name
    || form.value.unitOfMeasurement !== i.unitOfMeasurement
    || n !== i.fallBelow
    || form.value.remarks !== (i.remarks ?? '')
  if (!changed) {
    toast.error('No form field changed!')
    return
  }
  update.mutate()
  isEditing.value = false
}

function onKeydown(event: KeyboardEvent) {
  if (!isEditing.value)
    return
  if (event.key === 'Escape') {
    event.preventDefault()
    cancel()
  }
  else if (event.key === 'Enter' && event.target instanceof HTMLInputElement) {
    event.preventDefault()
    save()
  }
}
</script>

<template>
  <TableRow @keydown="onKeydown">
    <TableCell data-label="ID" class="tabular-nums">
      {{ ingredient.id }}
    </TableCell>
    <TableCell data-label="Name" class="min-w-36 font-semibold">
      <Input v-if="isEditing" ref="firstInput" v-model="form.name" aria-label="Active ingredient name" />
      <template v-else>
        {{ ingredient.name }}
      </template>
    </TableCell>
    <TableCell data-label="Unit" class="min-w-28">
      <Input v-if="isEditing" v-model="form.unitOfMeasurement" aria-label="Unit of measurement" />
      <template v-else>
        {{ ingredient.unitOfMeasurement }}
      </template>
    </TableCell>
    <TableCell data-label="Fall Below" class="min-w-24 tabular-nums">
      <template v-if="isEditing">
        <NumericInput v-model="form.fallBelow" step="1" min="1" aria-label="Fall below" :aria-invalid="!!fallBelowError || undefined" />
        <p v-if="fallBelowError" class="mt-0.5 text-xs text-destructive touch:text-sm" role="alert">
          {{ fallBelowError }}
        </p>
      </template>
      <template v-else>
        {{ ingredient.fallBelow }}
      </template>
    </TableCell>
    <TableCell data-label="Remarks" class="min-w-36">
      <Input v-if="isEditing" v-model="form.remarks" aria-label="Remarks" />
      <template v-else>
        {{ ingredient.remarks }}
      </template>
    </TableCell>
    <TableCell data-stack="actions">
      <div class="flex justify-end gap-1">
        <template v-if="isEditing">
          <Button size="sm" variant="success" :disabled="update.isPending.value" @click="save">
            <PhFloppyDisk aria-hidden="true" /> {{ update.isPending.value ? 'Saving...' : 'Save' }}
          </Button>
          <Button size="sm" variant="outline" @click="cancel">
            <PhX aria-hidden="true" /> Cancel
          </Button>
        </template>
        <template v-else>
          <Button size="sm" variant="outline" :aria-label="`Edit ${ingredient.name}`" @click="startEditing">
            <PhPencilSimple aria-hidden="true" /> Edit
          </Button>
          <Button size="sm" variant="destructive" :aria-label="`Delete ${ingredient.name}`" @click="isDeleting = true">
            <PhTrash aria-hidden="true" /> Delete
          </Button>
        </template>
      </div>
      <AppDialog v-if="isDeleting" title="Confirm Deletion?" size="sm" @close="isDeleting = false">
        <dl class="grid grid-cols-[8rem_1fr] gap-y-1">
          <dt class="text-muted-foreground">
            Active Ingredient:
          </dt><dd class="font-semibold">
            {{ ingredient.name }}
          </dd>
          <dt class="text-muted-foreground">
            Unit:
          </dt><dd>{{ ingredient.unitOfMeasurement }}</dd>
          <dt class="text-muted-foreground">
            Fall Below:
          </dt><dd>{{ ingredient.fallBelow }}</dd>
          <dt class="text-muted-foreground">
            Remarks:
          </dt><dd>{{ ingredient.remarks }}</dd>
        </dl>
        <template #footer>
          <Button variant="outline" @click="isDeleting = false">
            Cancel
          </Button>
          <Button variant="destructive" :disabled="remove.isPending.value" @click="remove.mutate()">
            <PhTrash aria-hidden="true" /> Confirm
          </Button>
        </template>
      </AppDialog>
    </TableCell>
  </TableRow>
</template>

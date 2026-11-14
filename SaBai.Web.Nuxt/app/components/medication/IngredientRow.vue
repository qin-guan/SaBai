<script setup lang="ts">
import { PhFloppyDisk, PhPencilSimple, PhTrash, PhX } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { deleteMedicationIngredient, updateMedicationIngredient } from '~/offline/mutations/medication'
import type { MedicationIngredient } from '~/offline/mutations/medication'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { TableCell, TableRow } from '@/components/ui/table'

/** One active ingredient; inline edit (Esc cancels, Enter saves) and delete with confirmation. */
const props = defineProps<{ ingredient: MedicationIngredient }>()

const isEditing = ref(false)
const isDeleting = ref(false)
const updateMutation = useOfflineMutation(updateMedicationIngredient)
const removeMutation = useOfflineMutation(deleteMedicationIngredient)
const brands = useMedicationBrands()
const initial = () => ({
  name: props.ingredient.name,
  unitOfMeasurement: props.ingredient.unitOfMeasurement,
  fallBelow: String(props.ingredient.fallBelow ?? ''),
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

async function remove() {
  if (removeMutation.isPending.value)
    return
  // The API refuses this too; checking the cached brands also covers working offline.
  if (brands.data.value?.some(b => b.activeIngredientId === props.ingredient.id)) {
    toast.error('Error! Check that there are no brands that depend on this active ingredient.')
    return
  }
  try {
    const outcome = await removeMutation.mutateAsync({ id: props.ingredient.id, name: props.ingredient.name })
    isDeleting.value = false
    notifySaved(outcome, 'Successfully deleted.')
  }
  catch (err) {
    console.error(err)
    toast.error(getApiErrorMessage(err, 'Error! Check that there are no brands that depend on this active ingredient.'))
  }
}

async function save() {
  if (updateMutation.isPending.value)
    return
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

  try {
    const outcome = await updateMutation.mutateAsync({
      id: i.id,
      name: form.value.name.trim(),
      unitOfMeasurement: form.value.unitOfMeasurement.trim(),
      fallBelow: n,
      remarks: form.value.remarks.trim() || null,
    })
    isEditing.value = false
    notifySaved(outcome, 'Successfully updated!')
  }
  catch (err) {
    console.error(err)
    toast.error(getApiErrorMessage(err, 'An error has occurred.'))
    form.value = initial()
  }
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
          <Button size="sm" variant="success" :disabled="updateMutation.isPending.value" @click="save">
            <PhFloppyDisk aria-hidden="true" /> {{ updateMutation.isPending.value ? 'Saving...' : 'Save' }}
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
          <Button variant="destructive" :disabled="removeMutation.isPending.value" @click="remove()">
            <PhTrash aria-hidden="true" /> Confirm
          </Button>
        </template>
      </AppDialog>
    </TableCell>
  </TableRow>
</template>

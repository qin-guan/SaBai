<script setup lang="ts">
import { PhFloppyDisk, PhPencilSimple, PhTrash, PhX } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { deleteMedicationIngredient, updateMedicationIngredient } from '~/offline/mutations/medication'
import type { MedicationIngredient } from '~/offline/mutations/medication'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { TableCell, TableRow } from '@/components/ui/table'

/** One active ingredient; inline edit (Esc cancels, Enter saves) and delete with confirmation. */
const props = defineProps<{
  ingredient: MedicationIngredient
  /** What can be ordered now (active batches), when the stock list is loaded. */
  available?: number
  /** Names of the ingredient's brands. */
  brandNames?: string[]
}>()

const isLow = computed(() => props.available !== undefined && props.available <= props.ingredient.fallBelow)

const isEditing = ref(false)
const isDeleting = ref(false)
const deleteError = ref<string | null>(null)
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
    deleteError.value = 'This active ingredient still has brands. Delete or move its brands first.'
    return
  }
  try {
    const outcome = await removeMutation.mutateAsync({ id: props.ingredient.id, name: props.ingredient.name })
    isDeleting.value = false
    notifySaved(outcome, `Deleted ${props.ingredient.name}.`)
  }
  catch (err) {
    console.error(err)
    // The row was taken off the list optimistically (and this dialog with it), so the refusal is a toast.
    toast.error(getApiErrorMessage(err, 'The active ingredient could not be deleted. Check that no brands depend on it.'))
    isDeleting.value = false
  }
}

async function save() {
  if (updateMutation.isPending.value)
    return
  const n = Number(form.value.fallBelow)
  fallBelowError.value = form.value.fallBelow === '' ? 'Enter the low-stock level' : (!Number.isInteger(n) || n < 1) ? 'Must be a whole number of at least 1' : null
  if (fallBelowError.value)
    return
  const i = props.ingredient
  const changed = form.value.name !== i.name
    || form.value.unitOfMeasurement !== i.unitOfMeasurement
    || n !== i.fallBelow
    || form.value.remarks !== (i.remarks ?? '')
  if (!changed) {
    isEditing.value = false
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
    notifySaved(outcome, `${form.value.name.trim()} updated.`)
  }
  catch (err) {
    console.error(err)
    toast.error(getApiErrorMessage(err, 'The active ingredient could not be updated.'))
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
  <TableRow :data-ingredient-id="ingredient.id" @keydown="onKeydown">
    <TableCell data-stack="title" class="min-w-36 font-semibold">
      <Input v-if="isEditing" ref="firstInput" v-model="form.name" aria-label="Active ingredient name" />
      <template v-else>
        {{ ingredient.name }}
      </template>
    </TableCell>
    <TableCell data-label="Counted in" class="min-w-24">
      <Input v-if="isEditing" v-model="form.unitOfMeasurement" aria-label="Unit of measurement" />
      <template v-else>
        {{ quantityUnit(ingredient.unitOfMeasurement) || '-' }}
      </template>
    </TableCell>
    <TableCell data-label="Low at" class="min-w-28 text-right whitespace-nowrap tabular-nums">
      <template v-if="isEditing">
        <NumericInput
          v-model="form.fallBelow"
          step="1"
          min="1"
          inputmode="numeric"
          :unit="quantityUnit(form.unitOfMeasurement, form.fallBelow.trim() ? Number(form.fallBelow) : null) || undefined"
          aria-label="Low-stock level"
          :aria-invalid="!!fallBelowError || undefined"
        />
        <p v-if="fallBelowError" class="mt-0.5 text-left text-xs text-destructive touch:text-sm" role="alert">
          {{ fallBelowError }}
        </p>
      </template>
      <template v-else>
        {{ formatQuantity(ingredient.fallBelow, ingredient.unitOfMeasurement) }}
      </template>
    </TableCell>
    <TableCell data-label="Available now" class="text-right whitespace-nowrap tabular-nums">
      <template v-if="props.available !== undefined">
        <span class="font-semibold" :class="isLow && 'text-destructive'">{{ formatQuantity(props.available, ingredient.unitOfMeasurement) }}</span>
        <span
          v-if="isLow"
          class="ml-1.5 inline-flex items-center border border-destructive/40 bg-destructive-subtle px-1.5 py-0.5 text-[0.6875rem] font-semibold text-destructive"
        >{{ props.available <= 0 ? 'Out' : 'Low' }}</span>
      </template>
    </TableCell>
    <TableCell data-label="Brands" class="max-w-64 min-w-32 whitespace-normal">
      <template v-if="props.brandNames?.length">
        {{ props.brandNames.join(', ') }}
      </template>
      <span v-else class="text-muted-foreground">No brands yet</span>
    </TableCell>
    <TableCell data-label="Remarks" class="max-w-64 min-w-32 whitespace-normal" :class="!isEditing && !ingredient.remarks && 'max-md:hidden!'">
      <Input v-if="isEditing" v-model="form.remarks" aria-label="Remarks" />
      <template v-else>
        {{ ingredient.remarks }}
      </template>
    </TableCell>
    <TableCell data-stack="actions">
      <div class="flex justify-end gap-1">
        <template v-if="isEditing">
          <Button size="sm" variant="success" :disabled="updateMutation.isPending.value" @click="save">
            <PhFloppyDisk aria-hidden="true" /> {{ updateMutation.isPending.value ? 'Saving…' : 'Save' }}
          </Button>
          <Button size="sm" variant="outline" @click="cancel">
            <PhX aria-hidden="true" /> Cancel
          </Button>
        </template>
        <template v-else>
          <Button size="sm" variant="outline" :aria-label="`Edit ${ingredient.name}`" @click="startEditing">
            <PhPencilSimple aria-hidden="true" /> Edit
          </Button>
          <Button size="sm" variant="outline" class="text-destructive hover:text-destructive" :aria-label="`Delete ${ingredient.name}`" @click="isDeleting = true; deleteError = null">
            <PhTrash aria-hidden="true" /> Delete
          </Button>
        </template>
      </div>
      <AppDialog v-if="isDeleting" title="Delete this active ingredient?" size="sm" @close="isDeleting = false">
        <dl class="grid grid-cols-[8rem_1fr] gap-y-1">
          <dt class="text-muted-foreground">
            Active ingredient
          </dt><dd class="font-semibold">
            {{ ingredient.name }}
          </dd>
          <dt class="text-muted-foreground">
            Counted in
          </dt><dd>{{ quantityUnit(ingredient.unitOfMeasurement) }}</dd>
          <dt class="text-muted-foreground">
            Low at
          </dt><dd>{{ formatQuantity(ingredient.fallBelow, ingredient.unitOfMeasurement) }}</dd>
          <template v-if="ingredient.remarks">
            <dt class="text-muted-foreground">
              Remarks
            </dt><dd>{{ ingredient.remarks }}</dd>
          </template>
        </dl>
        <p class="mt-2 text-xs text-muted-foreground touch:text-sm">
          An active ingredient that still has brands cannot be deleted.
        </p>
        <FormErrorNote v-if="deleteError" :message="deleteError" class="mt-2" />
        <template #footer>
          <Button variant="outline" @click="isDeleting = false">
            Keep it
          </Button>
          <Button variant="destructive" :disabled="removeMutation.isPending.value" @click="remove()">
            <PhTrash aria-hidden="true" /> Delete
          </Button>
        </template>
      </AppDialog>
    </TableCell>
  </TableRow>
</template>

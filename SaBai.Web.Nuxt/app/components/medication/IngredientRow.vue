<script setup lang="ts">
import { PhFloppyDisk, PhPencilSimple, PhTrash, PhX } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import type { LocalMedicationIngredient } from '~/composables/useMedication'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { TableCell, TableRow } from '@/components/ui/table'

/** One active ingredient; inline edit (Esc cancels, Enter saves) and delete with confirmation. */
const props = defineProps<{ ingredient: LocalMedicationIngredient }>()

const isEditing = ref(false)
const isDeleting = ref(false)
const isSaving = ref(false)
const isRemoving = ref(false)
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
  if (isRemoving.value)
    return
  isRemoving.value = true
  try {
    const db = getPowerSyncInstance()
    const brandCount = await db.getOptional<{ c: number }>(
      'SELECT COUNT(*) AS c FROM medication_brands WHERE active_ingredient_id = ?',
      [props.ingredient.id],
    )
    if (Number(brandCount?.c ?? 0) > 0) {
      toast.error('Error! Check that there are no brands that depend on this active ingredient.')
      return
    }
    await db.execute('DELETE FROM medication_active_ingredients WHERE id = ?', [props.ingredient.id])
    await refreshPendingCrudCount()
    toast.success('Successfully deleted.')
    isDeleting.value = false
  }
  catch (err) {
    console.error(err)
    toast.error('Error! Check that there are no brands that depend on this active ingredient.')
  }
  finally {
    isRemoving.value = false
  }
}

async function save() {
  if (isSaving.value)
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

  isSaving.value = true
  try {
    const db = getPowerSyncInstance()
    await db.execute(
      `UPDATE medication_active_ingredients
       SET name = ?, unit_of_measurement = ?, fall_below = ?, remarks = ?
       WHERE id = ?`,
      [
        form.value.name.trim(),
        form.value.unitOfMeasurement.trim(),
        n,
        form.value.remarks.trim() || null,
        props.ingredient.id,
      ],
    )
    await refreshPendingCrudCount()
    toast.success('Successfully updated!')
    isEditing.value = false
  }
  catch (err) {
    console.error(err)
    toast.error('An error has occurred.')
    form.value = initial()
  }
  finally {
    isSaving.value = false
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
          <Button size="sm" variant="success" :disabled="isSaving" @click="save">
            <PhFloppyDisk aria-hidden="true" /> {{ isSaving ? 'Saving...' : 'Save' }}
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
          <Button variant="destructive" :disabled="isRemoving" @click="remove()">
            <PhTrash aria-hidden="true" /> Confirm
          </Button>
        </template>
      </AppDialog>
    </TableCell>
  </TableRow>
</template>

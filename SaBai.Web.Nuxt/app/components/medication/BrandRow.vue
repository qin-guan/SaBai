<script setup lang="ts">
import { PhFloppyDisk, PhPencilSimple, PhTrash, PhX } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { deleteMedicationBrand, updateMedicationBrand } from '~/offline/mutations/medication'
import type { MedicationBrand, MedicationIngredient } from '~/offline/mutations/medication'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { TableCell, TableRow } from '@/components/ui/table'

/** One medication brand; inline edit (Esc cancels, Enter saves) and delete with confirmation. */
const props = defineProps<{
  brand: MedicationBrand
  ingredients: MedicationIngredient[]
}>()

const ingredientName = computed(() => {
  const i = props.ingredients.find(x => x.id === props.brand.activeIngredientId)
  return i ? `${i.name} (${i.unitOfMeasurement})` : 'Unknown ingredient'
})

const isEditing = ref(false)
const isDeleting = ref(false)
const updateMutation = useOfflineMutation(updateMedicationBrand)
const removeMutation = useOfflineMutation(deleteMedicationBrand)
const initial = () => ({
  name: props.brand.name,
  activeIngredientId: props.brand.activeIngredientId,
  remarks: props.brand.remarks ?? '',
})
const form = ref(initial())
const nameError = ref<string | null>(null)
const firstInput = ref<InstanceType<typeof Input>>()

function startEditing() {
  form.value = initial()
  isEditing.value = true
  nextTick(() => (firstInput.value?.$el as HTMLInputElement | undefined)?.focus())
}
function cancel() {
  form.value = initial()
  nameError.value = null
  isEditing.value = false
}

async function remove() {
  if (removeMutation.isPending.value)
    return
  try {
    // Whether stock or orders still use the brand is checked by the API.
    const outcome = await removeMutation.mutateAsync({ id: props.brand.id, name: props.brand.name })
    isDeleting.value = false
    notifySaved(outcome, 'Successfully deleted.')
  }
  catch (err) {
    console.error(err)
    toast.error(getApiErrorMessage(err, 'Error! Check that no stock or orders use this brand.'))
    isDeleting.value = false
  }
}

async function save() {
  if (updateMutation.isPending.value)
    return
  nameError.value = form.value.name.trim() ? null : 'Missing name!'
  if (nameError.value)
    return
  const b = props.brand
  const changed = form.value.name.trim() !== b.name
    || form.value.activeIngredientId !== b.activeIngredientId
    || form.value.remarks !== (b.remarks ?? '')
  if (!changed) {
    toast.error('No form field changed!')
    return
  }

  const ingredient = props.ingredients.find(x => x.id === form.value.activeIngredientId)
  if (!ingredient) {
    toast.error('Select an active ingredient.')
    return
  }
  try {
    const outcome = await updateMutation.mutateAsync({
      id: b.id,
      name: form.value.name.trim(),
      activeIngredientId: ingredient.id,
      remarks: form.value.remarks.trim(),
      ingredientName: ingredient.name,
      ingredientFallBelow: ingredient.fallBelow,
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
      {{ brand.id }}
    </TableCell>
    <TableCell data-label="Name" class="min-w-36 font-semibold">
      <template v-if="isEditing">
        <Input ref="firstInput" v-model="form.name" aria-label="Brand name" :aria-invalid="!!nameError || undefined" />
        <p v-if="nameError" class="mt-0.5 text-xs text-destructive touch:text-sm" role="alert">
          {{ nameError }}
        </p>
      </template>
      <template v-else>
        {{ brand.name }}
      </template>
    </TableCell>
    <TableCell data-label="Active Ingredient" class="min-w-44">
      <NativeSelect v-if="isEditing" v-model="form.activeIngredientId" aria-label="Active ingredient">
        <NativeSelectOption v-for="i in ingredients" :key="i.id" :value="i.id">
          {{ i.name }} ({{ i.unitOfMeasurement }})
        </NativeSelectOption>
      </NativeSelect>
      <NuxtLink v-else :to="paths.medicationActiveIngredients()" class="hover:text-primary hover:underline">
        {{ ingredientName }}
      </NuxtLink>
    </TableCell>
    <TableCell data-label="Remarks" class="min-w-36">
      <Input v-if="isEditing" v-model="form.remarks" aria-label="Remarks" />
      <template v-else>
        {{ brand.remarks }}
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
          <Button size="sm" variant="outline" :aria-label="`Edit ${brand.name}`" @click="startEditing">
            <PhPencilSimple aria-hidden="true" /> Edit
          </Button>
          <Button size="sm" variant="destructive" :aria-label="`Delete ${brand.name}`" @click="isDeleting = true">
            <PhTrash aria-hidden="true" /> Delete
          </Button>
        </template>
      </div>
      <AppDialog v-if="isDeleting" title="Confirm Deletion?" size="sm" @close="isDeleting = false">
        <dl class="grid grid-cols-[8rem_1fr] gap-y-1">
          <dt class="text-muted-foreground">
            Brand:
          </dt><dd class="font-semibold">
            {{ brand.name }}
          </dd>
          <dt class="text-muted-foreground">
            Active Ingredient:
          </dt><dd>{{ ingredientName }}</dd>
          <dt class="text-muted-foreground">
            Remarks:
          </dt><dd>{{ brand.remarks }}</dd>
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

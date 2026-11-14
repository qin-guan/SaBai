<script setup lang="ts">
import { PhArrowsSplit, PhFloppyDisk, PhPencilSimple, PhX } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { updateMedicationStock } from '~/offline/mutations/medication'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { TableCell, TableRow } from '@/components/ui/table'

/**
 * One stock line. Only location, status and remarks are editable inline; the
 * other fields are locked. Esc cancels, Enter / Ctrl+S saves while editing.
 */
const props = defineProps<{ item: MedicationStockRow }>()
const emit = defineEmits<{ split: [item: MedicationStockRow] }>()

const isEditing = ref(false)
const update = useOfflineMutation(updateMedicationStock)
const initial = () => ({
  location: props.item.location,
  stockStatus: props.item.stockStatus as string,
  remarks: props.item.remarks ?? '',
})
const form = ref(initial())
const locationError = ref<string | null>(null)
const isDirty = computed(() => JSON.stringify(form.value) !== JSON.stringify(initial()))
const firstInput = ref<InstanceType<typeof Input>>()
const editButton = ref<InstanceType<typeof Button>>()

function startEditing() {
  form.value = initial()
  isEditing.value = true
  nextTick(() => (firstInput.value?.$el as HTMLInputElement | undefined)?.focus())
}

function cancel() {
  isEditing.value = false
  form.value = initial()
  locationError.value = null
  nextTick(() => (editButton.value?.$el as HTMLElement | undefined)?.focus())
}

async function save() {
  if (update.isPending.value)
    return
  locationError.value = form.value.location.trim() ? null : 'Missing Location!'
  if (locationError.value)
    return
  if (!isDirty.value) {
    toast.error('No fields changed!')
    return
  }

  try {
    const outcome = await update.mutateAsync({
      id: props.item.id,
      location: form.value.location.trim(),
      stockStatus: form.value.stockStatus as StockStatus,
      remarks: form.value.remarks.trim() || null,
      brandName: props.item.brandName,
    })
    isEditing.value = false
    notifySaved(outcome, 'Successfully updated!')
  }
  catch (err) {
    console.error(err)
    toast.error(getApiErrorMessage(err, 'An error has occurred while updating the stock. Refresh and try again.'))
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
  <TableRow
    :class="item.isLowStock && 'bg-destructive-subtle! hover:bg-destructive-subtle/70!'"
    @keydown="onKeydown"
  >
    <TableCell data-stack="title" class="font-semibold">
      <NuxtLink :to="paths.medicationActiveIngredients()" class="hover:text-primary hover:underline">
        {{ item.ingredientName }}
      </NuxtLink>
      <Badge v-if="item.isLowStock" variant="destructive" class="ml-1">
        Low
      </Badge>
    </TableCell>
    <TableCell data-label="Brand">
      <NuxtLink :to="paths.medicationBrands()" class="hover:text-primary hover:underline">
        {{ item.brandName }}
      </NuxtLink>
    </TableCell>
    <TableCell data-label="Location" class="min-w-36">
      <template v-if="isEditing">
        <Input
          ref="firstInput"
          v-model="form.location"
          :aria-label="`Location for ${item.brandName}`"
          :aria-invalid="!!locationError || undefined"
        />
        <p v-if="locationError" class="mt-0.5 text-xs text-destructive touch:text-sm" role="alert">
          {{ locationError }}
        </p>
      </template>
      <template v-else>
        {{ item.location }}
      </template>
    </TableCell>
    <TableCell data-label="Quantity" class="text-right tabular-nums">
      {{ item.quantity }}
    </TableCell>
    <TableCell data-label="Expiry" class="tabular-nums">
      {{ item.expiry ? new Date(item.expiry).toLocaleDateString() : '-' }}
    </TableCell>
    <TableCell data-label="State" class="min-w-32">
      <NativeSelect v-if="isEditing" v-model="form.stockStatus" :aria-label="`Status for ${item.brandName}`">
        <NativeSelectOption v-for="o in stockStatusOptions" :key="o.value" :value="o.value">
          {{ o.label }}
        </NativeSelectOption>
      </NativeSelect>
      <template v-else>
        {{ item.stockStatus }}
      </template>
    </TableCell>
    <TableCell data-label="Remarks" class="min-w-36">
      <Input v-if="isEditing" v-model="form.remarks" :aria-label="`Remarks for ${item.brandName}`" />
      <template v-else>
        {{ item.remarks }}
      </template>
    </TableCell>
    <TableCell data-stack="actions">
      <div class="flex justify-end gap-1">
        <template v-if="isEditing">
          <Button size="sm" variant="success" :disabled="update.isPending.value" @click="save">
            <PhFloppyDisk aria-hidden="true" /> {{ update.isPending.value ? 'Saving…' : 'Save' }}
          </Button>
          <Button size="sm" variant="outline" @click="cancel">
            <PhX aria-hidden="true" /> Cancel
          </Button>
        </template>
        <template v-else>
          <Button ref="editButton" size="sm" variant="outline" :aria-label="`Edit ${item.brandName} at ${item.location}`" @click="startEditing">
            <PhPencilSimple aria-hidden="true" /> Edit
          </Button>
          <Button size="sm" variant="outline" :aria-label="`Split ${item.brandName} at ${item.location}`" @click="emit('split', item)">
            <PhArrowsSplit aria-hidden="true" /> Split
          </Button>
        </template>
      </div>
    </TableCell>
  </TableRow>
</template>

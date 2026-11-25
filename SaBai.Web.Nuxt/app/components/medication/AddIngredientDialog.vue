<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import { createMedicationIngredient } from '~/offline/mutations/medication'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

/**
 * Adds an active ingredient: its name (with strength), the unit every
 * quantity of it is counted in, and the level at or below which it is flagged
 * as low stock.
 */
const emit = defineEmits<{ close: [] }>()
const form = ref({ name: '', unitOfMeasurement: '', fallBelow: '', remarks: '' })
const errors = ref<{ name?: string, unitOfMeasurement?: string, fallBelow?: string }>({})
const serverError = ref<string | null>(null)
const formEl = ref<HTMLFormElement>()
const create = useOfflineMutation(createMedicationIngredient)

const fallBelowUnit = computed(() =>
  quantityUnit(form.value.unitOfMeasurement, form.value.fallBelow.trim() ? Number(form.value.fallBelow) : null) || undefined)

async function submit() {
  if (create.isPending.value)
    return
  serverError.value = null
  const n = Number(form.value.fallBelow)
  errors.value = {
    name: form.value.name.trim() ? undefined : 'Enter the name',
    unitOfMeasurement: form.value.unitOfMeasurement.trim() ? undefined : 'Enter the unit, e.g. tablet or mL',
    fallBelow: form.value.fallBelow === '' ? 'Enter the low-stock level' : (!Number.isInteger(n) || n < 1) ? 'Must be a whole number of at least 1' : undefined,
  }
  if (Object.values(errors.value).some(Boolean)) {
    nextTick(() => focusFirstInvalid(formEl.value))
    return
  }

  try {
    // The id is chosen here so a create queued offline can be referenced right away.
    const outcome = await create.mutateAsync({
      id: uuidv7(),
      name: form.value.name.trim(),
      unitOfMeasurement: form.value.unitOfMeasurement.trim(),
      fallBelow: n,
      remarks: form.value.remarks.trim() || null,
    })
    emit('close')
    notifySaved(outcome, `Added ${form.value.name.trim()}.`)
  }
  catch (err) {
    serverError.value = getApiErrorMessage(err, 'The active ingredient could not be added.')
  }
}

useSaveShortcut(submit)
</script>

<template>
  <AppDialog title="Add active ingredient" size="sm" @close="emit('close')">
    <form id="add-ingredient-form" ref="formEl" novalidate class="flex flex-col gap-3" @submit.prevent="submit">
      <FormErrorNote v-if="serverError" :message="serverError" />
      <FormField v-slot="{ id, describedby, invalid }" label="Name" required :error="errors.name" hint="With the strength, e.g. Paracetamol 500mg">
        <Input :id="id" v-model="form.name" :aria-describedby="describedby" :aria-invalid="invalid || undefined" />
      </FormField>
      <FormField v-slot="{ id, describedby, invalid }" label="Counted in" required :error="errors.unitOfMeasurement" hint="One unit, singular: tablet, capsule, mL, sachet, bottle…">
        <Input :id="id" v-model="form.unitOfMeasurement" autocapitalize="off" :aria-describedby="describedby" :aria-invalid="invalid || undefined" />
      </FormField>
      <FormField v-slot="{ id, describedby, invalid }" label="Low-stock level" required :error="errors.fallBelow" hint="Flagged as low when this many or fewer are available.">
        <NumericInput
          :id="id"
          v-model="form.fallBelow"
          step="1"
          min="1"
          inputmode="numeric"
          :unit="fallBelowUnit"
          :aria-describedby="describedby"
          :aria-invalid="invalid || undefined"
        />
      </FormField>
      <FormField v-slot="{ id }" label="Remarks">
        <Input :id="id" v-model="form.remarks" />
      </FormField>
    </form>
    <template #footer>
      <span class="mr-auto hidden text-xs text-muted-foreground kb:inline touch:text-sm">
        <ShortcutHint keys="Ctrl+S" /> save · <ShortcutHint keys="Esc" /> close
      </span>
      <Button variant="outline" @click="emit('close')">
        Cancel
      </Button>
      <Button type="submit" form="add-ingredient-form" variant="success" :disabled="create.isPending.value">
        <PhFloppyDisk aria-hidden="true" /> {{ create.isPending.value ? 'Saving…' : 'Add active ingredient' }}
      </Button>
    </template>
  </AppDialog>
</template>

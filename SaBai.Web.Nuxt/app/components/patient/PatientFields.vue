<script setup lang="ts">
import { Input } from '@/components/ui/input'
import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'

import type { PatientFormValues } from '~/utils/patientForm'

/** Patient demographic fields shared by registration and "Edit Patient Details". */
const model = defineModel<PatientFormValues>({ required: true })
const props = defineProps<{ errors: Partial<Record<keyof PatientFormValues, string>> }>()

const textFields = [
  { key: 'name', label: 'Name', type: 'text', autocomplete: 'off' },
  { key: 'identificationNumber', label: 'Identification Number', type: 'text', autocomplete: 'off' },
  { key: 'contactNo', label: 'Contact Number', type: 'tel', autocomplete: 'off' },
] as const

const cards = [
  { key: 'hasPoorCard', label: 'Has POOR Card?' },
  { key: 'hasBS2Card', label: 'Has BS2 Card?' },
  { key: 'hasSabaiCard', label: 'Has Sabai Card?' },
] as const
</script>

<template>
  <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
    <FormField
      v-for="f in textFields"
      :key="f.key"
      v-slot="{ id, describedby, invalid }"
      :label="f.label"
      required
      :error="props.errors[f.key]"
    >
      <Input
        :id="id"
        v-model="model[f.key]"
        :type="f.type"
        :autocomplete="f.autocomplete"
        :aria-describedby="describedby"
        :aria-invalid="invalid || undefined"
        :data-field="f.key"
      />
    </FormField>
    <FormField v-slot="{ id, describedby, invalid }" label="Gender" required :error="props.errors.gender">
      <NativeSelect
        :id="id"
        v-model="model.gender"
        :aria-describedby="describedby"
        :aria-invalid="invalid || undefined"
        data-field="gender"
      >
        <NativeSelectOption value="">
          Select an option
        </NativeSelectOption>
        <NativeSelectOption value="male">
          Male
        </NativeSelectOption>
        <NativeSelectOption value="female">
          Female
        </NativeSelectOption>
      </NativeSelect>
    </FormField>
    <FormField v-slot="{ id, describedby, invalid }" label="Drug Allergy" required :error="props.errors.drugAllergy">
      <Input
        :id="id"
        v-model="model.drugAllergy"
        :aria-describedby="describedby"
        :aria-invalid="invalid || undefined"
        data-field="drugAllergy"
      />
    </FormField>
    <FormField v-slot="{ id, describedby, invalid }" label="Date of Birth" required :error="props.errors.dateOfBirth">
      <Input
        :id="id"
        v-model="model.dateOfBirth"
        type="date"
        :aria-describedby="describedby"
        :aria-invalid="invalid || undefined"
        data-field="dateOfBirth"
      />
    </FormField>
    <fieldset class="border border-border sm:col-span-2">
      <legend class="sr-only">
        Cards
      </legend>
      <div class="grid grid-cols-1 divide-y divide-border sm:grid-cols-3 sm:divide-x sm:divide-y-0">
        <div v-for="c in cards" :key="c.key" class="flex min-h-9 items-center gap-2 px-3 py-1.5 [@media(pointer:coarse)]:min-h-11">
          <Checkbox :id="`pf-${c.key}`" v-model="model[c.key]" />
          <Label :for="`pf-${c.key}`" class="flex-1 cursor-pointer">{{ c.label }}</Label>
        </div>
      </div>
    </fieldset>
  </div>
</template>

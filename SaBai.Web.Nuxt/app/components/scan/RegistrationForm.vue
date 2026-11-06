<script setup lang="ts">
import { PhMagnifyingGlass, PhUserPlus } from '@phosphor-icons/vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import { saBaiAPIEndpointsPatientsCreatePostEndpoint } from '@sabai/sdk/clients'
import { Button } from '@/components/ui/button'

/** Registers a new patient with the captured photo, in the selected village. */
const image = defineModel<string | null>('image', { required: true })
const emit = defineEmits<{ match: [] }>()

const form = ref<PatientFormValues>(blankPatientForm())
// The photo is not kept (too large for storage); the typed details are.
const draft = useFormDraft('register-patient', form)
form.value = draft.resume(blankPatientForm())
const errors = ref<Partial<Record<keyof PatientFormValues, string>>>({})
const formEl = ref<HTMLFormElement>()
const { selectedVillageCodeId } = useVillageCode()

const queryClient = useQueryClient()
const create = useMutation({
  mutationFn: (values: PatientFormValues) =>
    saBaiAPIEndpointsPatientsCreatePostEndpoint({
      ...apiConfig,
      body: {
        name: values.name,
        identificationNumber: values.identificationNumber,
        contactNo: values.contactNo,
        gender: values.gender as 'male' | 'female',
        dateOfBirth: new Date(values.dateOfBirth).toISOString(),
        drugAllergy: values.drugAllergy,
        hasPoorCard: values.hasPoorCard,
        hasBS2Card: values.hasBS2Card,
        hasSabaiCard: values.hasSabaiCard,
        toGetReport: false,
        villageCodeId: selectedVillageCodeId.value!,
      },
      throwOnError: true,
    }).unwrap(),
})

async function submit() {
  if (create.isPending.value)
    return
  errors.value = validatePatientForm(form.value)
  if (Object.keys(errors.value).length) {
    focusFirstInvalid(formEl.value)
    return
  }
  if (!image.value) {
    toast.error('Please capture a face image before submitting.')
    return
  }
  if (!selectedVillageCodeId.value) {
    toast.error('Please select a village code before submitting.')
    return
  }

  let patient
  try {
    patient = await create.mutateAsync(form.value)
  }
  catch (error) {
    console.error('Error creating patient:', error)
    toast.error(getApiErrorMessage(error, 'Failed to create patient. Please try again.'))
    return
  }

  queryClient.invalidateQueries({ queryKey: queryKeys.patients() })

  // Best-effort: the patient already exists, so a failure here must not discard the registration.
  try {
    await setPatientPhotoAndFace(patient.id, image.value)
  }
  catch (error) {
    console.error('Error saving patient photo/face:', error)
    toast.error('Patient created, but the photo or face could not be saved. You can retry from the patient page.')
  }

  toast.success('Patient created successfully!')
  image.value = null
  form.value = blankPatientForm()
  draft.clear(form.value)
  errors.value = {}
}

useSaveShortcut(submit)
</script>

<template>
  <AppPanel title="Register new patient">
    <template #actions>
      <Button size="sm" variant="outline" @click="emit('match')">
        <PhMagnifyingGlass aria-hidden="true" /> Match Instead
      </Button>
    </template>
    <DraftNotice :restored-at="draft.restoredAt.value" @discard="form = draft.discard()" />
    <form ref="formEl" novalidate @submit.prevent="submit"
    @keydown.enter="enterMovesToNextField">
      <PatientFields v-model="form" :errors="errors" />
      <SaveBar>
        <Button type="submit" variant="success" :disabled="create.isPending.value">
          <PhUserPlus aria-hidden="true" />
          {{ create.isPending.value ? 'Creating new patient...' : 'Create New Patient' }}
        </Button>
      </SaveBar>
    </form>
  </AppPanel>
</template>

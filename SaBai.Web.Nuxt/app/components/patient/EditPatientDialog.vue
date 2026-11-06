<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import { saBaiAPIEndpointsPatientsUpdatePatchEndpoint } from '@sabai/sdk/clients'
import type { SaBaiAPIEndpointsPatientsGetResponse } from '@sabai/sdk/types'
import { Button } from '@/components/ui/button'

/**
 * Edits an existing patient's details. Optionally captures a new photo, which
 * replaces the profile photo and re-enrolls the patient's face.
 */
const props = defineProps<{ patient: SaBaiAPIEndpointsPatientsGetResponse }>()
const emit = defineEmits<{ close: [] }>()

const form = ref<PatientFormValues>({
  name: props.patient.name,
  identificationNumber: props.patient.identificationNumber ?? '',
  contactNo: props.patient.contactNo ?? '',
  gender: props.patient.gender,
  drugAllergy: props.patient.drugAllergy,
  // <input type="date"> only accepts yyyy-MM-dd.
  dateOfBirth: toDateInputValue(props.patient.dateOfBirth),
  hasPoorCard: props.patient.hasPoorCard,
  hasBS2Card: props.patient.hasBS2Card,
  hasSabaiCard: props.patient.hasSabaiCard,
})
const errors = ref<Partial<Record<keyof PatientFormValues, string>>>({})
const newPhoto = ref<string | null>(null)
const formEl = ref<HTMLFormElement>()

const queryClient = useQueryClient()
const update = useMutation({
  mutationFn: (values: PatientFormValues) =>
    saBaiAPIEndpointsPatientsUpdatePatchEndpoint({
      ...apiConfig,
      path: { id: props.patient.id },
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
      },
      throwOnError: true,
    }).unwrap(),
})

async function submit() {
  if (update.isPending.value)
    return
  errors.value = validatePatientForm(form.value)
  if (Object.keys(errors.value).length) {
    focusFirstInvalid(formEl.value)
    return
  }

  try {
    await update.mutateAsync(form.value)
  }
  catch (error) {
    console.error('Error updating patient:', error)
    // Surface a known conflict (duplicate ID) directly; otherwise a generic message.
    toast.error(
      getApiErrorStatus(error) === 409
        ? getApiErrorMessage(error, 'Failed to update patient. Please try again.')
        : 'Failed to update patient. Please try again.',
    )
    return
  }

  if (newPhoto.value) {
    try {
      await setPatientPhotoAndFace(props.patient.id, newPhoto.value)
      queryClient.invalidateQueries({ queryKey: queryKeys.patientPhoto(props.patient.id) })
      newPhoto.value = null
    }
    catch (error) {
      console.error('Error saving patient photo/face:', error)
      toast.error(getApiErrorMessage(error, 'Patient updated, but the photo or face could not be saved.'))
    }
  }

  queryClient.invalidateQueries({ queryKey: queryKeys.patients() })
  toast.success('Patient details updated successfully!')
  emit('close')
}

useSaveShortcut(submit)
</script>

<template>
  <AppDialog title="Edit Patient Details" size="lg" @close="emit('close')">
    <form id="edit-patient-form" ref="formEl" novalidate class="flex flex-col gap-4" @submit.prevent="submit">
      <PatientFields v-model="form" :errors="errors" />
      <AppPanel title="Profile photo & face">
        <p class="mb-3 text-xs text-muted-foreground">
          {{ props.patient.hasPhoto
            ? 'Capture a new photo to replace the current one and re-enroll the patient\'s face.'
            : 'Capture a photo to set the patient\'s profile photo and enroll their face.' }}
        </p>
        <WebcamCapture v-model="newPhoto" :auto-open-camera="false" :size="240" />
      </AppPanel>
    </form>
    <template #footer>
      <span class="mr-auto hidden text-xs text-muted-foreground [@media(pointer:fine)]:inline">
        <ShortcutHint keys="Ctrl+S" /> save · <ShortcutHint keys="Esc" /> cancel
      </span>
      <Button variant="outline" @click="emit('close')">
        Cancel
      </Button>
      <Button type="submit" form="edit-patient-form" variant="success" :disabled="update.isPending.value">
        <PhFloppyDisk aria-hidden="true" />
        {{ update.isPending.value ? 'Saving...' : 'Save Changes' }}
      </Button>
    </template>
  </AppDialog>
</template>

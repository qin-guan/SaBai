import { useQueryClient } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import type { PatientDetail } from '~/composables/usePatientData'
import { updatePatient } from '~/offline/mutations/patients'
import type { PatientFormValues } from '~/utils/patientForm'

function toFormValues(patient: PatientDetail): PatientFormValues {
  return {
    name: patient.name,
    identificationNumber: patient.identificationNumber ?? '',
    contactNo: patient.contactNo ?? '',
    gender: patient.gender,
    drugAllergy: patient.drugAllergy,
    // <input type="date"> only accepts yyyy-MM-dd.
    dateOfBirth: toDateInputValue(patient.dateOfBirth),
    hasPoorCard: patient.hasPoorCard,
    hasBS2Card: patient.hasBS2Card,
    hasSabaiCard: patient.hasSabaiCard,
    tags: [...(patient.tags ?? [])],
  }
}

/**
 * State and save logic for editing a patient's details, shared by the inline
 * form on the patient record and the "Edit Patient Details" dialog on station
 * screens. Unsaved input is kept as a draft; an optional new photo replaces
 * the profile photo and re-enrolls the face.
 */
export function usePatientEditForm(
  patient: () => PatientDetail,
  formEl: Ref<HTMLElement | undefined>,
) {
  const initial = toFormValues(patient())
  const form = ref<PatientFormValues>({ ...initial })
  const draft = useFormDraft(`edit-patient:${patient().id}`, form)
  form.value = draft.resume(initial)
  const baseline = ref(JSON.stringify(initial))
  const errors = ref<Partial<Record<keyof PatientFormValues, string>>>({})
  const newPhoto = ref<string | null>(null)
  const isDirty = computed(() => JSON.stringify(form.value) !== baseline.value || !!newPhoto.value)

  // Pick up changes saved elsewhere (another station, another device) while
  // there is nothing unsaved here.
  watch(patient, (next) => {
    if (isDirty.value)
      return
    const values = toFormValues(next)
    form.value = values
    baseline.value = JSON.stringify(values)
    draft.clear(values)
  })

  const queryClient = useQueryClient()
  const { mutateAsync, isPending } = useOfflineMutation(updatePatient)
  const { isOnline } = useSyncState()

  /** Saves; resolves true when the details were saved (or queued on this device). */
  async function submit(): Promise<boolean> {
    if (isPending.value)
      return false
    errors.value = validatePatientForm(form.value)
    if (Object.keys(errors.value).length) {
      focusFirstInvalid(formEl.value)
      return false
    }

    const id = patient().id
    const values = form.value
    let outcome
    try {
      outcome = await mutateAsync({
        id,
        name: values.name,
        identificationNumber: values.identificationNumber,
        contactNo: values.contactNo,
        gender: values.gender as 'male' | 'female',
        dateOfBirth: new Date(values.dateOfBirth).toISOString(),
        drugAllergy: values.drugAllergy,
        hasPoorCard: values.hasPoorCard,
        hasBS2Card: values.hasBS2Card,
        hasSabaiCard: values.hasSabaiCard,
        tags: values.tags,
      })
    }
    catch (error) {
      console.error('Error updating patient:', error)
      // Surface a known conflict (duplicate ID, changed by someone else) directly; otherwise a generic message.
      const status = getApiErrorStatus(error)
      toast.error(
        status === 409 || status === 412
          ? getApiErrorMessage(error, 'Failed to update patient. Please try again.')
          : 'Failed to update patient. Please try again.',
      )
      return false
    }

    if (newPhoto.value) {
      // The photo and face enrolment need the server, and the patient must exist there.
      if (outcome.queued || !isOnline.value) {
        toast.warning('Patient details are saved, but the photo and face enrolment need a connection. Add the photo again once online.')
      }
      else {
        try {
          const face = await setPatientPhotoAndFace(id, newPhoto.value)
          queryClient.invalidateQueries({ queryKey: queryKeys.patientPhoto(id) })
          queryClient.invalidateQueries({ queryKey: queryKeys.patients() })
          if (!face.enrolled)
            toast.warning(`Photo saved, but the face was not enrolled: ${face.reason}`, { description: FACE_CONSENT_HINT })
        }
        catch (error) {
          console.error('Error saving patient photo/face:', error)
          toast.error(getApiErrorMessage(error, 'Patient updated, but the photo or face could not be saved.'))
        }
      }
      newPhoto.value = null
    }

    baseline.value = JSON.stringify(form.value)
    draft.clear(form.value)
    notifySaved(outcome, 'Patient details updated successfully!')
    return true
  }

  function reset() {
    form.value = draft.discard()
    newPhoto.value = null
    errors.value = {}
  }

  return { form, draft, errors, newPhoto, isDirty, isSaving: isPending, submit, reset }
}

import { useQueryClient } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import type { LocalPatientSummary } from '~/db/types'
import type { PatientFormValues } from '~/utils/patientForm'

function toFormValues(patient: LocalPatientSummary): PatientFormValues {
  return {
    name: patient.name,
    identificationNumber: patient.identificationNumber ?? '',
    contactNo: patient.contactNo ?? '',
    gender: patient.gender === 'other' ? '' : patient.gender,
    drugAllergy: patient.drugAllergy,
    // <input type="date"> only accepts yyyy-MM-dd.
    dateOfBirth: toDateInputValue(patient.dateOfBirth),
    hasPoorCard: patient.hasPoorCard,
    hasBS2Card: patient.hasBS2Card,
    hasSabaiCard: patient.hasSabaiCard,
  }
}

/**
 * State and save logic for editing a patient's details, shared by the inline
 * form on the patient record and the "Edit Patient Details" dialog on station
 * screens. Unsaved input is kept as a draft; an optional new photo replaces
 * the profile photo and re-enrolls the face.
 */
export function usePatientEditForm(
  patient: () => LocalPatientSummary,
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
  const isSaving = ref(false)

  /** Saves; resolves true when the details were saved. */
  async function submit(): Promise<boolean> {
    if (isSaving.value)
      return false
    errors.value = validatePatientForm(form.value)
    if (Object.keys(errors.value).length) {
      focusFirstInvalid(formEl.value)
      return false
    }

    const current = patient()
    const values = form.value
    isSaving.value = true
    try {
      const db = getPowerSyncInstance()
      const dob = new Date(values.dateOfBirth).toISOString()
      const filterString = `${values.name} ${values.identificationNumber} ${values.contactNo}`.trim()
      await db.execute(
        `UPDATE patients
         SET name = ?,
             identification_number = ?,
             contact_no = ?,
             gender = ?,
             date_of_birth = ?,
             drug_allergy = ?,
             has_poor_card = ?,
             has_bs2_card = ?,
             has_sabai_card = ?,
             filter_string = ?
         WHERE id = ?`,
        [
          values.name.trim(),
          values.identificationNumber.trim() || null,
          values.contactNo.trim() || null,
          values.gender,
          dob,
          values.drugAllergy.trim() || 'None',
          values.hasPoorCard ? 1 : 0,
          values.hasBS2Card ? 1 : 0,
          values.hasSabaiCard ? 1 : 0,
          filterString,
          current.id,
        ],
      )
      await refreshPendingCrudCount()
    }
    catch (error) {
      console.error('Error updating patient:', error)
      toast.error('Failed to update patient. Please try again.')
      return false
    }
    finally {
      isSaving.value = false
    }

    const id = current.id
    if (newPhoto.value) {
      if (typeof navigator !== 'undefined' && !navigator.onLine) {
        toast.warning('Patient updated locally. Photo and face enrollment require an internet connection.')
      }
      else {
        try {
          await setPatientPhotoAndFace(id, newPhoto.value)
          queryClient.invalidateQueries({ queryKey: queryKeys.patientPhoto(id) })
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
    toast.success('Patient details updated!')
    return true
  }

  function reset() {
    form.value = draft.discard()
    newPhoto.value = null
    errors.value = {}
  }

  return { form, draft, errors, newPhoto, isDirty, isSaving, submit, reset }
}

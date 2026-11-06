export interface PatientFormValues {
  name: string
  identificationNumber: string
  contactNo: string
  gender: '' | 'male' | 'female'
  drugAllergy: string
  dateOfBirth: string
  hasPoorCard: boolean
  hasBS2Card: boolean
  hasSabaiCard: boolean
}

const LABELS: Record<string, string> = {
  name: 'Name',
  identificationNumber: 'Identification Number',
  contactNo: 'Contact Number',
  gender: 'Gender',
  drugAllergy: 'Drug Allergy',
  dateOfBirth: 'Date of Birth',
}

export const blankPatientForm = (): PatientFormValues => ({
  name: '',
  identificationNumber: '',
  contactNo: '',
  gender: '',
  drugAllergy: '',
  dateOfBirth: '',
  hasPoorCard: false,
  hasBS2Card: false,
  hasSabaiCard: false,
})

/** Every demographic field is required (same rule as SaBai.Web: "Missing <label>!"). */
export function validatePatientForm(values: PatientFormValues) {
  const errors: Partial<Record<keyof PatientFormValues, string>> = {}
  for (const key of Object.keys(LABELS) as (keyof PatientFormValues)[]) {
    if (!String(values[key] ?? '').trim())
      errors[key] = `Missing ${LABELS[key]}!`
  }
  return errors
}

/** Moves focus to the first invalid field inside `root`. */
export function focusFirstInvalid(root?: HTMLElement | null) {
  nextTick(() => {
    const el = (root ?? document).querySelector<HTMLElement>('[aria-invalid="true"]')
    el?.focus()
  })
}

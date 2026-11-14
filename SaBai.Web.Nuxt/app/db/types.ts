import type { Journey } from '~/utils/journey'

export interface LocalVillageCode {
  id: string
  version: number
  code: string
  name: string
  colorHex: string
  isVisible: boolean
  createdAt: string
}

export interface LocalPatientSummary {
  id: string
  number: number
  displayId: string
  filterString: string
  villageCodeId: string | null
  villageCode: string | null
  villageColorHex?: string
  name: string
  identificationNumber: string | null
  contactNo: string | null
  gender: 'male' | 'female' | 'other'
  dateOfBirth: string
  drugAllergy: string
  hasPoorCard: boolean
  hasBS2Card: boolean
  hasSabaiCard: boolean
  toGetReport: boolean
  hasPhoto: boolean
  hasFace: boolean
  patientImagePublicId: string | null
  lastVisitId: string | null
  lastVisitDate: string | null
  createdAt: string
  tags: string[]
}

export interface LocalVisitSummary {
  id: string
  patientId: string
  patientName: string
  villageCodeId: string
  villageCode: string | null
  villageName: string | null
  villageColorHex?: string
  date: string
  status: string
  journey: Journey
}

export interface LocalPatientWithLatestVisit extends LocalPatientSummary {
  latestVisit: LocalVisitSummary | null
  lastVisitJourney: Journey | null
}

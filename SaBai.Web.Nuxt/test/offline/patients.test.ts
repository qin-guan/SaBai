import { QueryClient } from '@tanstack/vue-query'
import { describe, expect, it } from 'vitest'
import {
  newRegisterPatientVars,
  newStartVisitVars,
  normalizeTags,
  patientsToPrefetch,
  pendingDisplayId,
  registerPatient,
  startVisit,
  updatePatient,
  type PatientDetail,
  type PatientWithLatestVisitItem,
} from '~/offline/mutations/patients'
import { queryKeys } from '~/utils/api'

const village = { villageCodeId: 'v1', code: 'PC', name: 'Phnom Chhat', colorHex: '#ff0000' }

const existing: PatientDetail = {
  id: 'p-old',
  name: 'Old Patient',
  identificationNumber: null,
  contactNo: null,
  gender: 'female',
  dateOfBirth: '1980-01-01T00:00:00.000Z',
  drugAllergy: 'None',
  hasPoorCard: false,
  hasBS2Card: false,
  hasSabaiCard: false,
  toGetReport: false,
  villageCodeId: 'v1',
  villageCode: 'PC',
  displayId: 'PC0001',
  filterString: 'pc0001 old patient',
  lastVisitDate: '2026-01-01T00:00:00.000Z',
  lastVisitId: 'visit-old',
  hasPhoto: false,
  hasFace: false,
  tags: [],
}

function seededClient() {
  const qc = new QueryClient()
  qc.setQueryData(queryKeys.patientsList(), { patients: [existing] })
  qc.setQueryData(queryKeys.patientsWithLatestVisit(), {
    patients: [{ ...existing, villageColorHex: '#ff0000', lastVisitJourney: null }],
  })
  qc.setQueryData(queryKeys.tags(), ['Elderly'])
  return qc
}

const fields = {
  name: '  Sok Dara ',
  identificationNumber: '',
  contactNo: '012 345 678',
  gender: 'male' as const,
  dateOfBirth: '1990-05-05T00:00:00.000Z',
  drugAllergy: 'None',
  hasPoorCard: false,
  hasBS2Card: false,
  hasSabaiCard: true,
  tags: ['diabetic', 'Diabetic', ' Elderly '],
}

describe('registerPatient (optimistic)', () => {
  it('shows the new patient in the detail, list, worklist and visits queries', () => {
    const qc = seededClient()
    const vars = newRegisterPatientVars(fields, village)
    registerPatient.optimistic!(vars, qc)

    const detail = qc.getQueryData<PatientDetail>(queryKeys.patient(vars.id))!
    expect(detail.name).toBe('Sok Dara')
    expect(detail.displayId).toBe(pendingDisplayId('PC'))
    expect(detail.lastVisitId).toBe(vars.visitId)
    expect(detail.tags).toEqual(['diabetic', 'Elderly'])

    expect(qc.getQueryData<{ patients: unknown[] }>(queryKeys.patientsList())!.patients).toHaveLength(2)
    const worklist = qc.getQueryData<{ patients: PatientWithLatestVisitItem[] }>(queryKeys.patientsWithLatestVisit())!
    expect(worklist.patients[0]!.id).toBe(vars.id)
    expect(worklist.patients[0]!.lastVisitJourney).toMatchObject({ vitals: false, pharmacyOrders: 0 })

    const visits = qc.getQueryData<{ visits: Array<{ id: string, patientId: string, villageName: string }> }>(queryKeys.patientVisits(vars.id))!
    expect(visits.visits).toEqual([expect.objectContaining({ id: vars.visitId, patientId: vars.id, villageName: 'Phnom Chhat' })])
    expect(qc.getQueryData<string[]>(queryKeys.tags())).toEqual(['diabetic', 'Elderly'])
  })

  it('is idempotent', () => {
    const qc = seededClient()
    const vars = newRegisterPatientVars(fields, village)
    registerPatient.optimistic!(vars, qc)
    const once = JSON.stringify([...qc.getQueryCache().getAll()].map(q => [q.queryKey, q.state.data]))
    registerPatient.optimistic!(vars, qc)
    expect(JSON.stringify([...qc.getQueryCache().getAll()].map(q => [q.queryKey, q.state.data]))).toBe(once)
  })

  it('works when no query is cached yet', () => {
    const qc = new QueryClient()
    const vars = newRegisterPatientVars(fields, village)
    registerPatient.optimistic!(vars, qc)
    expect(qc.getQueryData(queryKeys.patient(vars.id))).toBeDefined()
    expect(qc.getQueryData(queryKeys.patientsList())).toBeUndefined()
  })

  it('lists every id it creates so later writes stay behind it', () => {
    const vars = newRegisterPatientVars(fields, village, { temperature: 36.5 })
    expect(registerPatient.recordIds!(vars)).toEqual([vars.id, vars.visitId, vars.vitalsId])
  })
})

describe('startVisit (optimistic)', () => {
  it('adds the visit and makes it the latest', () => {
    const qc = seededClient()
    const vars = newStartVisitVars('p-old', 'Old Patient', village)
    startVisit.optimistic!(vars, qc)
    const visits = qc.getQueryData<{ visits: Array<{ id: string }> }>(queryKeys.patientVisits('p-old'))!
    expect(visits.visits.map(v => v.id)).toEqual([vars.id])
    expect(qc.getQueryData<PatientDetail>(queryKeys.patient('p-old'))).toBeUndefined()
    const worklist = qc.getQueryData<{ patients: PatientWithLatestVisitItem[] }>(queryKeys.patientsWithLatestVisit())!
    expect(worklist.patients[0]).toMatchObject({ id: 'p-old', lastVisitId: vars.id })
    startVisit.optimistic!(vars, qc)
    expect(qc.getQueryData<{ visits: unknown[] }>(queryKeys.patientVisits('p-old'))!.visits).toHaveLength(1)
  })
})

describe('updatePatient (optimistic)', () => {
  it('edits the patient everywhere it is cached without touching the input', () => {
    const qc = seededClient()
    qc.setQueryData(queryKeys.patient('p-old'), existing)
    const before = structuredClone(existing)
    updatePatient.optimistic!({ ...fields, id: 'p-old', name: 'New Name', tags: ['Fresh'] }, qc)
    expect(existing).toEqual(before)
    expect(qc.getQueryData<PatientDetail>(queryKeys.patient('p-old'))).toMatchObject({ name: 'New Name', tags: ['Fresh'], gender: 'male', contactNo: '012 345 678' })
    expect(qc.getQueryData<{ patients: PatientDetail[] }>(queryKeys.patientsList())!.patients[0]!.name).toBe('New Name')
    expect(qc.getQueryData<string[]>(queryKeys.tags())).toEqual(['Elderly', 'Fresh'])
  })
})

describe('patientsToPrefetch', () => {
  const now = new Date('2026-11-14T12:00:00Z').getTime()
  const hoursAgo = (h: number) => new Date(now - h * 3600_000).toISOString()
  it('takes patients with a visit in the last day, newest first, up to the limit', () => {
    const patients = [
      { id: 'old', lastVisitDate: hoursAgo(30) },
      { id: 'none', lastVisitDate: null },
      { id: 'a', lastVisitDate: hoursAgo(5) },
      { id: 'b', lastVisitDate: hoursAgo(1) },
      { id: 'c', lastVisitDate: hoursAgo(10) },
    ]
    expect(patientsToPrefetch(patients, now)).toEqual(['b', 'a', 'c'])
    expect(patientsToPrefetch(patients, now, 2)).toEqual(['b', 'a'])
  })
})

describe('normalizeTags', () => {
  it('trims, drops blanks and duplicates (ignoring case) and sorts', () => {
    expect(normalizeTags([' b', 'A', 'a', '', '  '])).toEqual(['A', 'b'])
  })
})

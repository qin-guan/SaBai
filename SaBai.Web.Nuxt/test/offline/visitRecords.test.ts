import { describe, expect, it } from 'vitest'
import {
  createConsult,
  createEyesight,
  createVitals,
  saveDental,
  updateEyesight,
  updateVitals,
  type CreateConsultVars,
  type SaveVitalsVars,
} from '~/offline/mutations/visitRecords'
import { getOfflineMutation } from '~/offline/defineOfflineMutation'
import { queryKeys } from '~/utils/api'
import { newQueryClient } from './helpers'

const journey = { vitals: false, vision: false, consult: false, dental: false, dentalReferred: false, pharmacyOrders: 0, pharmacyPending: 0 }

const vitalsVars = (fields: SaveVitalsVars['fields'], id = 'v-1'): SaveVitalsVars => ({
  id,
  visitId: 'visit-1',
  recordedAt: '2026-11-14T08:00:00.000Z',
  fields,
})

describe('visit record mutations', () => {
  it('registers every definition by key', () => {
    for (const def of [createVitals, updateVitals, createEyesight, updateEyesight, saveDental, createConsult])
      expect(getOfflineMutation(def.key)).toBe(def)
  })

  it('a vitals create fills the per-visit query and marks the journey step', () => {
    const queryClient = newQueryClient()
    queryClient.setQueryData(['patients', 'visits', 'p-1'], { visits: [{ id: 'visit-1', journey }, { id: 'visit-2', journey }] })
    queryClient.setQueryData(queryKeys.patientsWithLatestVisit(), { patients: [{ id: 'p-1', lastVisitId: 'visit-1', lastVisitJourney: journey }] })
    const vars = vitalsVars({ height: 150, weight: null, others: 'ok' })

    createVitals.optimistic!(vars, queryClient)
    createVitals.optimistic!(vars, queryClient)

    expect(queryClient.getQueryData(queryKeys.vitals('visit-1'))).toMatchObject({ id: 'v-1', visitId: 'visit-1', height: 150, weight: null, others: 'ok' })
    const visits = queryClient.getQueryData<{ visits: { id: string, journey: typeof journey & { vitalsAt?: string } }[] }>(['patients', 'visits', 'p-1'])!.visits
    expect(visits[0]!.journey).toMatchObject({ vitals: true, vitalsAt: vars.recordedAt })
    expect(visits[1]!.journey.vitals).toBe(false)
    expect(queryClient.getQueryData<{ patients: { lastVisitJourney: { vitals: boolean } }[] }>(queryKeys.patientsWithLatestVisit())!.patients[0]!.lastVisitJourney.vitals).toBe(true)
  })

  it('a vitals update keeps the saved record id and only applies set fields, like the PATCH', () => {
    const queryClient = newQueryClient()
    const saved = { id: 'server-id', visitId: 'visit-1', height: 150, weight: 50 }
    queryClient.setQueryData(queryKeys.vitals('visit-1'), saved)

    updateVitals.optimistic!(vitalsVars({ height: 151, weight: null }, 'server-id'), queryClient)

    expect(queryClient.getQueryData(queryKeys.vitals('visit-1'))).toMatchObject({ id: 'server-id', height: 151, weight: 50 })
    expect(saved.height).toBe(150)
  })

  it('an eyesight edit leaves unset fields alone', () => {
    const queryClient = newQueryClient()
    queryClient.setQueryData(queryKeys.eyesight('visit-1'), { id: 'e-1', visitId: 'visit-1', leftEyeDegree: '6/9', comments: 'old' })

    updateEyesight.optimistic!({ id: 'e-1', visitId: 'visit-1', recordedAt: 'x', fields: { rightEyeDegree: '6/6' } }, queryClient)

    expect(queryClient.getQueryData(queryKeys.eyesight('visit-1'))).toMatchObject({ id: 'e-1', leftEyeDegree: '6/9', rightEyeDegree: '6/6', comments: 'old' })
  })

  it('a dental save replaces the record and keeps an existing id', () => {
    const queryClient = newQueryClient()
    queryClient.setQueryData(queryKeys.dental('visit-1'), { id: 'd-1', visitId: 'visit-1', chiefComplaint: 'old', diagnoses: ['a'] })

    saveDental.optimistic!({ id: 'new', visitId: 'visit-1', recordedAt: 'x', fields: { chiefComplaint: 'pain', diagnoses: ['b'] } }, queryClient)

    expect(queryClient.getQueryData(queryKeys.dental('visit-1'))).toMatchObject({ id: 'd-1', chiefComplaint: 'pain', diagnoses: ['b'] })
  })

  it('a consult is added once, first, to the visit list and its own query', () => {
    const queryClient = newQueryClient()
    queryClient.setQueryData(queryKeys.consults('visit-1'), [{ id: 'older', visitId: 'visit-1' }])
    const vars: CreateConsultVars = {
      id: 'c-1',
      visitId: 'visit-1',
      doctorId: 'u-1',
      doctorNickname: 'Dr A',
      date: '2026-11-14T09:00:00.000Z',
      consultation: 'cough',
      diagnoses: [{ id: 'dx-1', details: 'cold', category: 'Other' as never }],
    }

    createConsult.optimistic!(vars, queryClient)
    createConsult.optimistic!(vars, queryClient)

    const list = queryClient.getQueryData<{ id: string, diagnoses: { id: string, consultId: string }[] }[]>(queryKeys.consults('visit-1'))!
    expect(list.map(c => c.id)).toEqual(['c-1', 'older'])
    expect(list[0]!.diagnoses).toEqual([expect.objectContaining({ id: 'dx-1', consultId: 'c-1' })])
    expect(queryClient.getQueryData(queryKeys.consult('c-1'))).toMatchObject({ id: 'c-1', doctorNickname: 'Dr A' })
  })

  it('writes its own record and depends on the visit (and patient, when known)', () => {
    expect(createVitals.recordIds!(vitalsVars({}, 'v-1'))).toEqual(['v-1'])
    expect(createVitals.creates).toBe(true)
    expect(createVitals.dependsOn!(vitalsVars({}, 'v-1'))).toEqual(['visit-1'])
    // With the patient known, a waiting or failed registration holds it back too.
    expect(createVitals.dependsOn!({ ...vitalsVars({}, 'v-1'), patientId: 'p-1' })).toEqual(['visit-1', 'p-1'])
    expect(updateVitals.baseQueries!(vitalsVars({}, 'v-1'))).toEqual([queryKeys.vitals('visit-1')])
  })
})

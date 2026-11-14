import type { QueryClient } from '@tanstack/vue-query'
import {
  saBaiAPIEndpointsConsultsCreatePostEndpoint,
  saBaiAPIEndpointsConsultsGetEndpoint,
  saBaiAPIEndpointsConsultsListByVisitGetEndpoint,
  saBaiAPIEndpointsDentalGetByVisitGetEndpoint,
  saBaiAPIEndpointsDentalUpsertByVisitPutEndpoint,
  saBaiAPIEndpointsEyesightCreatePostEndpoint,
  saBaiAPIEndpointsEyesightGetByVisitGetEndpoint,
  saBaiAPIEndpointsEyesightUpdateByVisitPatchEndpoint,
  saBaiAPIEndpointsVitalsCreatePostEndpoint,
  saBaiAPIEndpointsVitalsGetByVisitGetEndpoint,
  saBaiAPIEndpointsVitalsUpdateByVisitPatchEndpoint,
} from '@sabai/sdk/clients'
import type {
  SaBaiAPIDomainDiagnosisCategoryKey,
  SaBaiAPIEndpointsConsultsGetResponse,
  SaBaiAPIEndpointsConsultsListByVisitGetResponse,
  SaBaiAPIEndpointsDentalDentalConsultResponse,
  SaBaiAPIEndpointsDentalUpsertByVisitPutRequest,
  SaBaiAPIEndpointsEyesightCreatePostRequest,
  SaBaiAPIEndpointsEyesightGetByVisitGetResponse,
  SaBaiAPIEndpointsVitalsCreatePostRequest,
  SaBaiAPIEndpointsVitalsGetByVisitGetResponse,
} from '@sabai/sdk/types'
import { defineOfflineMutation, requestOptions } from '~/offline/defineOfflineMutation'
import { apiConfig, queryKeys } from '~/utils/api'

/**
 * Station records for one visit: vitals, eyesight, dental and consults.
 * Vitals, eyesight and dental hold one record per visit, so each is cached
 * under its per-visit key (`queryKeys.vitals(visitId)` …) which both the form
 * and the visit summary read; a consult is added to `queryKeys.consults(visitId)`.
 */

export type VisitVitals = SaBaiAPIEndpointsVitalsGetByVisitGetResponse
export type VisitEyesight = SaBaiAPIEndpointsEyesightGetByVisitGetResponse
export type VisitDental = SaBaiAPIEndpointsDentalDentalConsultResponse
export type VisitConsult = SaBaiAPIEndpointsConsultsListByVisitGetResponse

// ── Reads ────────────────────────────────────────────────────────────────

export const fetchVisitVitals = (visitId: string, signal?: AbortSignal) =>
  saBaiAPIEndpointsVitalsGetByVisitGetEndpoint({ ...apiConfig, path: { visitId }, signal }).unwrap()

export const fetchVisitEyesight = (visitId: string, signal?: AbortSignal) =>
  saBaiAPIEndpointsEyesightGetByVisitGetEndpoint({ ...apiConfig, path: { visitId }, signal }).unwrap()

export const fetchVisitDental = (visitId: string, signal?: AbortSignal) =>
  saBaiAPIEndpointsDentalGetByVisitGetEndpoint({ ...apiConfig, path: { visitId }, signal }).unwrap()

export const fetchVisitConsults = (visitId: string, signal?: AbortSignal) =>
  saBaiAPIEndpointsConsultsListByVisitGetEndpoint({ ...apiConfig, path: { visitId }, signal }).unwrap()

export const fetchConsult = (id: string, signal?: AbortSignal) =>
  saBaiAPIEndpointsConsultsGetEndpoint({ ...apiConfig, path: { id }, signal }).unwrap()

// ── Cache edits ──────────────────────────────────────────────────────────

type JourneyStation = 'vitals' | 'vision' | 'consult' | 'dental'

interface JourneyShape { [key: string]: unknown }
interface VisitWithJourney { id: string, journey?: JourneyShape }
interface PatientWithJourney { lastVisitId?: string | null, lastVisitJourney?: JourneyShape | null }

/** `journey` with `station` marked as recorded; keeps an earlier timestamp. */
function withStationDone<T extends JourneyShape>(journey: T, station: JourneyStation, at: string): T {
  if (journey[station] === true) return journey
  return { ...journey, [station]: true, [`${station}At`]: at }
}

/**
 * Marks the visit's journey step as recorded in every cached patient visit
 * list and station worklist, so the journey bars follow an offline save.
 * Lists that are not cached, or have another shape, are left alone.
 */
export function markJourneyDone(queryClient: QueryClient, visitId: string, station: JourneyStation, at: string) {
  queryClient.setQueriesData<{ visits?: VisitWithJourney[] }>(
    { queryKey: ['patients', 'visits'] },
    (old) => {
      if (!old || !Array.isArray(old.visits)) return old
      if (!old.visits.some(v => v.id === visitId && v.journey && v.journey[station] !== true)) return old
      return {
        ...old,
        visits: old.visits.map(v =>
          v.id === visitId && v.journey ? { ...v, journey: withStationDone(v.journey, station, at) } : v),
      }
    },
  )
  queryClient.setQueryData<{ patients?: PatientWithJourney[] }>(queryKeys.patientsWithLatestVisit(), (old) => {
    if (!old || !Array.isArray(old.patients)) return old
    if (!old.patients.some(p => p.lastVisitId === visitId && p.lastVisitJourney && p.lastVisitJourney[station] !== true)) return old
    return {
      ...old,
      patients: old.patients.map(p =>
        p.lastVisitId === visitId && p.lastVisitJourney
          ? { ...p, lastVisitJourney: withStationDone(p.lastVisitJourney, station, at) }
          : p),
    }
  })
}

/** Fields of `changes` that are set (not null/undefined): what a PATCH by visit applies. */
function definedFields<T extends object>(changes: T): Partial<T> {
  return Object.fromEntries(Object.entries(changes).filter(([, value]) => value != null)) as Partial<T>
}

/** A station record needs its visit and (when the screen knows it) its patient on the server first. */
const stationDependsOn = (vars: { visitId: string, patientId?: string }) =>
  [vars.visitId, ...(vars.patientId ? [vars.patientId] : [])]

const refreshAfterSave = async (queryClient: QueryClient, visitKey: readonly unknown[]) => {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: visitKey }),
    // Journey bars and station backlogs.
    queryClient.invalidateQueries({ queryKey: queryKeys.patients() }),
  ])
}

// ── Vitals ───────────────────────────────────────────────────────────────

export type VitalsFields = Omit<SaBaiAPIEndpointsVitalsCreatePostRequest, 'id' | 'visitId'>

export interface SaveVitalsVars {
  /** The visit's vitals record id: the cached one for an edit, a new `uuidv7()` for a create. */
  id: string
  visitId: string
  /** The visit's patient, when the screen knows it (see `dependsOn`). */
  patientId?: string
  /** Shown in the pending-changes list. */
  patientName?: string
  recordedAt: string
  fields: VitalsFields
}

const describeVitals = (vars: SaveVitalsVars) => `Vitals for ${vars.patientName ?? 'patient'}`

/** Vitals as cached after a create (every field present) or a PATCH by visit (set fields only). */
function vitalsRecord(vars: SaveVitalsVars, old: VisitVitals | undefined, patch: boolean): VisitVitals {
  const blank: VisitVitals = {
    id: vars.id,
    visitId: vars.visitId,
    height: null,
    weight: null,
    temperature: null,
    systolic: null,
    diastolic: null,
    heartRate: null,
    hemocueCount: null,
    diabetesMellitus: null,
    urineTest: null,
    bloodGlucoseNonFasting: null,
    bloodGlucoseFasting: null,
    hba1c: null,
    others: null,
  }
  const changes = patch ? definedFields(vars.fields) : vars.fields
  return { ...blank, ...old, ...changes, id: old?.id ?? vars.id, visitId: vars.visitId } as VisitVitals
}

function applyVitals(queryClient: QueryClient, vars: SaveVitalsVars, patch: boolean) {
  queryClient.setQueryData<VisitVitals>(queryKeys.vitals(vars.visitId), old => vitalsRecord(vars, old, patch))
  markJourneyDone(queryClient, vars.visitId, 'vitals', vars.recordedAt)
}

const afterVitalsSave = (saved: VisitVitals, vars: SaveVitalsVars, queryClient: QueryClient) => {
  queryClient.setQueryData<VisitVitals>(queryKeys.vitals(vars.visitId), saved)
  return refreshAfterSave(queryClient, queryKeys.vitals(vars.visitId))
}

export const createVitals = defineOfflineMutation({
  key: 'vitals.create',
  creates: true,
  describe: describeVitals,
  recordIds: (vars: SaveVitalsVars) => [vars.id],
  dependsOn: stationDependsOn,
  send: (vars: SaveVitalsVars, ctx) =>
    saBaiAPIEndpointsVitalsCreatePostEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      body: { ...vars.fields, id: vars.id, visitId: vars.visitId },
    }).unwrap(),
  optimistic: (vars, queryClient) => applyVitals(queryClient, vars, false),
  onSuccess: afterVitalsSave,
})

export const updateVitals = defineOfflineMutation({
  key: 'vitals.update',
  describe: describeVitals,
  recordIds: (vars: SaveVitalsVars) => [vars.id],
  dependsOn: stationDependsOn,
  // The form shows the visit's saved vitals.
  baseQueries: vars => [queryKeys.vitals(vars.visitId)],
  send: (vars: SaveVitalsVars, ctx) =>
    saBaiAPIEndpointsVitalsUpdateByVisitPatchEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      path: { visitId: vars.visitId },
      body: vars.fields,
    }).unwrap(),
  optimistic: (vars, queryClient) => applyVitals(queryClient, vars, true),
  onSuccess: afterVitalsSave,
})

// ── Eyesight ─────────────────────────────────────────────────────────────

export type EyesightFields = Omit<SaBaiAPIEndpointsEyesightCreatePostRequest, 'id' | 'visitId'>

export interface SaveEyesightVars {
  id: string
  visitId: string
  /** The visit's patient, when the screen knows it (see `dependsOn`). */
  patientId?: string
  patientName?: string
  recordedAt: string
  fields: EyesightFields
}

const describeEyesight = (vars: SaveEyesightVars) => `Vision for ${vars.patientName ?? 'patient'}`

function eyesightRecord(vars: SaveEyesightVars, old: VisitEyesight | undefined): VisitEyesight {
  const blank: VisitEyesight = {
    id: vars.id,
    visitId: vars.visitId,
    leftEyeDegree: null,
    rightEyeDegree: null,
    leftEyePinhole: null,
    rightEyePinhole: null,
    leftAstigmatism: null,
    rightAstigmatism: null,
    comments: null,
    leftPrescribedGlassesDegree: null,
    rightPrescribedGlassesDegree: null,
  }
  return { ...blank, ...old, ...definedFields(vars.fields), id: old?.id ?? vars.id, visitId: vars.visitId } as VisitEyesight
}

function applyEyesight(vars: SaveEyesightVars, queryClient: QueryClient) {
  queryClient.setQueryData<VisitEyesight>(queryKeys.eyesight(vars.visitId), old => eyesightRecord(vars, old))
  markJourneyDone(queryClient, vars.visitId, 'vision', vars.recordedAt)
}

const afterEyesightSave = (saved: VisitEyesight, vars: SaveEyesightVars, queryClient: QueryClient) => {
  queryClient.setQueryData<VisitEyesight>(queryKeys.eyesight(vars.visitId), saved)
  return refreshAfterSave(queryClient, queryKeys.eyesight(vars.visitId))
}

export const createEyesight = defineOfflineMutation({
  key: 'eyesight.create',
  creates: true,
  describe: describeEyesight,
  recordIds: (vars: SaveEyesightVars) => [vars.id],
  dependsOn: stationDependsOn,
  send: (vars: SaveEyesightVars, ctx) =>
    saBaiAPIEndpointsEyesightCreatePostEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      body: { ...vars.fields, id: vars.id, visitId: vars.visitId },
    }).unwrap(),
  optimistic: applyEyesight,
  onSuccess: afterEyesightSave,
})

export const updateEyesight = defineOfflineMutation({
  key: 'eyesight.update',
  describe: describeEyesight,
  recordIds: (vars: SaveEyesightVars) => [vars.id],
  dependsOn: stationDependsOn,
  // The form shows the visit's saved eyesight.
  baseQueries: vars => [queryKeys.eyesight(vars.visitId)],
  send: (vars: SaveEyesightVars, ctx) =>
    saBaiAPIEndpointsEyesightUpdateByVisitPatchEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      path: { visitId: vars.visitId },
      body: vars.fields,
    }).unwrap(),
  optimistic: applyEyesight,
  onSuccess: afterEyesightSave,
})

// ── Dental ───────────────────────────────────────────────────────────────

export type DentalFields = Omit<SaBaiAPIEndpointsDentalUpsertByVisitPutRequest, 'id'>

export interface SaveDentalVars {
  /** The visit's dental record id: the cached one when there is one, otherwise a new `uuidv7()`. */
  id: string
  visitId: string
  /** The visit's patient, when the screen knows it (see `dependsOn`). */
  patientId?: string
  patientName?: string
  recordedAt: string
  fields: DentalFields
}

/** The PUT replaces the whole record, so the cached copy becomes the request. */
function applyDental(vars: SaveDentalVars, queryClient: QueryClient) {
  queryClient.setQueryData<VisitDental>(queryKeys.dental(vars.visitId), old => ({
    ...old,
    ...vars.fields,
    diagnoses: vars.fields.diagnoses ?? [],
    id: old?.id ?? vars.id,
  }) as VisitDental)
  markJourneyDone(queryClient, vars.visitId, 'dental', vars.recordedAt)
}

export const saveDental = defineOfflineMutation({
  key: 'dental.save',
  describe: (vars: SaveDentalVars) => `Dental consultation for ${vars.patientName ?? 'patient'}`,
  recordIds: (vars: SaveDentalVars) => [vars.id],
  dependsOn: stationDependsOn,
  baseQueries: vars => [queryKeys.dental(vars.visitId)],
  send: (vars: SaveDentalVars, ctx) =>
    saBaiAPIEndpointsDentalUpsertByVisitPutEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      path: { visitId: vars.visitId },
      body: { ...vars.fields, id: vars.id },
    }).unwrap(),
  optimistic: applyDental,
  onSuccess: (saved, vars, queryClient) => {
    queryClient.setQueryData<VisitDental>(queryKeys.dental(vars.visitId), saved as VisitDental)
    return refreshAfterSave(queryClient, queryKeys.dental(vars.visitId))
  },
})

// ── Consults ─────────────────────────────────────────────────────────────

export interface ConsultDiagnosisVars {
  id: string
  details: string
  category: SaBaiAPIDomainDiagnosisCategoryKey
}

export interface CreateConsultVars {
  /** Client-chosen ids: the create endpoint is idempotent on them. */
  id: string
  visitId: string
  /** The visit's patient, when the screen knows it (see `dependsOn`). */
  patientId?: string
  patientName?: string
  /** Who writes the consult, for the optimistic copy. */
  doctorId: string
  doctorNickname: string
  /** When the consult was written (ISO). */
  date: string
  pastMedicalHistory?: string | null
  consultation?: string | null
  treatmentPlan?: string | null
  referredFor?: string | null
  referralNotes?: string | null
  remarks?: string | null
  diagnoses: ConsultDiagnosisVars[]
}

function consultRecord(vars: CreateConsultVars): VisitConsult & SaBaiAPIEndpointsConsultsGetResponse {
  return {
    id: vars.id,
    visitId: vars.visitId,
    date: vars.date,
    pastMedicalHistory: vars.pastMedicalHistory ?? null,
    consultation: vars.consultation ?? null,
    treatmentPlan: vars.treatmentPlan ?? null,
    referredFor: vars.referredFor ?? null,
    referralNotes: vars.referralNotes ?? null,
    remarks: vars.remarks ?? null,
    doctorId: vars.doctorId,
    doctorNickname: vars.doctorNickname,
    diagnoses: vars.diagnoses.map(d => ({ id: d.id, consultId: vars.id, details: d.details, category: d.category })),
  }
}

/** Puts the consult first in the visit's list (newest first), replacing an entry with the same id. */
function upsertConsult(queryClient: QueryClient, consult: VisitConsult) {
  queryClient.setQueryData<VisitConsult[]>(queryKeys.consults(consult.visitId), (old) => {
    if (!old) return [consult]
    const index = old.findIndex(c => c.id === consult.id)
    return index === -1 ? [consult, ...old] : old.map(c => (c.id === consult.id ? consult : c))
  })
  queryClient.setQueryData<SaBaiAPIEndpointsConsultsGetResponse>(queryKeys.consult(consult.id), consult)
}

export const createConsult = defineOfflineMutation({
  key: 'consults.create',
  creates: true,
  describe: (vars: CreateConsultVars) => `Consult for ${vars.patientName ?? 'patient'}`,
  recordIds: (vars: CreateConsultVars) => [vars.id, ...vars.diagnoses.map(d => d.id)],
  dependsOn: stationDependsOn,
  send: (vars: CreateConsultVars, ctx) =>
    saBaiAPIEndpointsConsultsCreatePostEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      body: {
        id: vars.id,
        visitId: vars.visitId,
        pastMedicalHistory: vars.pastMedicalHistory,
        consultation: vars.consultation,
        treatmentPlan: vars.treatmentPlan,
        referredFor: vars.referredFor,
        referralNotes: vars.referralNotes,
        remarks: vars.remarks,
        diagnoses: vars.diagnoses.map(d => ({ id: d.id, details: d.details, category: d.category })),
      },
    }).unwrap(),
  optimistic: (vars, queryClient) => {
    upsertConsult(queryClient, consultRecord(vars))
    markJourneyDone(queryClient, vars.visitId, 'consult', vars.date)
  },
  revert: (vars, queryClient) => {
    queryClient.setQueryData<VisitConsult[]>(queryKeys.consults(vars.visitId), old => old?.filter(c => c.id !== vars.id))
    queryClient.removeQueries({ queryKey: queryKeys.consult(vars.id), exact: true })
  },
  onSuccess: async (saved, vars, queryClient) => {
    upsertConsult(queryClient, saved as VisitConsult)
    await refreshAfterSave(queryClient, queryKeys.consults(vars.visitId))
  },
})

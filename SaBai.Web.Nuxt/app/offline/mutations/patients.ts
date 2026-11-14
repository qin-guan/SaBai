import type { QueryClient } from '@tanstack/vue-query'
import {
  saBaiAPIEndpointsAuthUsersListGetEndpoint,
  saBaiAPIEndpointsPatientsConsentsPostEndpoint,
  saBaiAPIEndpointsPatientsCreatePostEndpoint,
  saBaiAPIEndpointsPatientsDisclosuresPostEndpoint,
  saBaiAPIEndpointsPatientsGetEndpoint,
  saBaiAPIEndpointsPatientsListGetEndpoint,
  saBaiAPIEndpointsPatientsUpdatePatchEndpoint,
  saBaiAPIEndpointsPatientsVisitsGetEndpoint,
  saBaiAPIEndpointsPatientsWithLatestVisitGetEndpoint,
  saBaiAPIEndpointsTagsListGetEndpoint,
  saBaiAPIEndpointsVisitsCreatePostEndpoint,
} from '@sabai/sdk/clients'
import type {
  SaBaiAPIDomainConsentTypeKey,
  SaBaiAPIEndpointsPatientsCreatePostResponse,
  SaBaiAPIEndpointsPatientsCreatePostVitalsRequest,
  SaBaiAPIEndpointsPatientsGetResponse,
  SaBaiAPIEndpointsPatientsListGetItem,
  SaBaiAPIEndpointsPatientsUpdatePatchResponse,
  SaBaiAPIEndpointsPatientsVisitJourney,
  SaBaiAPIEndpointsPatientsVisitsGetItem,
  SaBaiAPIEndpointsPatientsVisitsGetResponse,
  SaBaiAPIEndpointsPatientsWithLatestVisitGetItem,
  SaBaiAPIEndpointsVisitsCreatePostResponse,
} from '@sabai/sdk/types'
import { defineOfflineMutation, requestOptions } from '~/offline/defineOfflineMutation'
import { CLINIC_STAFF_ROLES, registerOfflinePrefetch } from '~/offline/prefetch'
import { apiConfig, queryKeys } from '~/utils/api'
import { uuidv7 } from '~/utils/uuid'

/**
 * Patients, visits and registration: reads, offline-capable writes and the
 * offline download.
 *
 * A patient or visit created on this device gets its client id and an
 * optimistic copy in every query a following screen reads: the patient detail,
 * the patient list, the station worklist (latest visit) and the patient's
 * visits. That is what lets a patient registered without a connection be
 * opened at the next station on the same device.
 */

export type PatientDetail = SaBaiAPIEndpointsPatientsGetResponse & { tags?: string[] }
export type PatientListItem = SaBaiAPIEndpointsPatientsListGetItem & { tags?: string[] }
export type PatientWithLatestVisitItem = SaBaiAPIEndpointsPatientsWithLatestVisitGetItem & { tags?: string[] }
export type PatientVisit = SaBaiAPIEndpointsPatientsVisitsGetItem
export type PatientVisitJourney = SaBaiAPIEndpointsPatientsVisitJourney

type PatientsList = { patients: PatientListItem[] }
type PatientsWithLatestVisit = { patients: PatientWithLatestVisitItem[] }
type PatientVisits = SaBaiAPIEndpointsPatientsVisitsGetResponse

/** Card number shown for a patient the server has not numbered yet. */
const PENDING_CARD_NUMBER = 'NEW'

// ── Reads ────────────────────────────────────────────────────────────────

export function fetchPatientsList(signal?: AbortSignal) {
  return saBaiAPIEndpointsPatientsListGetEndpoint({ ...apiConfig, signal })
    .unwrap()
    .then(x => x as PatientsList)
}

export function fetchPatientsWithLatestVisit(signal?: AbortSignal) {
  return saBaiAPIEndpointsPatientsWithLatestVisitGetEndpoint({ ...apiConfig, signal })
    .unwrap()
    .then(x => x as PatientsWithLatestVisit)
}

export function fetchPatient(id: string, signal?: AbortSignal) {
  return saBaiAPIEndpointsPatientsGetEndpoint({ ...apiConfig, path: { id }, signal })
    .unwrap()
    .then(x => x as PatientDetail)
}

export function fetchPatientVisits(id: string, signal?: AbortSignal) {
  return saBaiAPIEndpointsPatientsVisitsGetEndpoint({ ...apiConfig, path: { patientId: id }, signal }).unwrap()
}

export function fetchTags(signal?: AbortSignal) {
  return saBaiAPIEndpointsTagsListGetEndpoint({ ...apiConfig, signal })
    .unwrap()
    .then(x => [...x.tags])
}

export function fetchStaffUsers(signal?: AbortSignal) {
  return saBaiAPIEndpointsAuthUsersListGetEndpoint({ ...apiConfig, signal }).unwrap()
}

/** How many patients' records are downloaded for offline use, newest visit first. */
export const PREFETCH_PATIENT_LIMIT = 150
const PREFETCH_WINDOW_MS = 24 * 60 * 60 * 1000
const PREFETCH_CONCURRENCY = 4

/**
 * The patients to download in full for offline use: those whose latest visit
 * is within the last day (a rolling window, so a clinic running past midnight
 * is covered), most recent first and at most `limit`.
 */
export function patientsToPrefetch(
  patients: readonly Pick<PatientWithLatestVisitItem, 'id' | 'lastVisitDate'>[],
  now = Date.now(),
  limit = PREFETCH_PATIENT_LIMIT,
): string[] {
  return patients
    .filter(p => p.lastVisitDate && now - new Date(p.lastVisitDate).getTime() < PREFETCH_WINDOW_MS)
    .sort((a, b) => new Date(b.lastVisitDate!).getTime() - new Date(a.lastVisitDate!).getTime())
    .slice(0, limit)
    .map(p => p.id)
}

/** Runs `fetchOne` for each of the recently seen patients, a few at a time; throws if any failed. */
async function prefetchRecentPatients(queryClient: QueryClient, what: string, fetchOne: (id: string) => Promise<unknown>) {
  const latest = await queryClient.ensureQueryData({
    queryKey: queryKeys.patientsWithLatestVisit(),
    queryFn: ({ signal }) => fetchPatientsWithLatestVisit(signal),
  })
  const queue = patientsToPrefetch(latest.patients)
  let failures = 0
  async function worker() {
    for (let id = queue.shift(); id; id = queue.shift()) {
      try {
        await fetchOne(id)
      }
      catch {
        failures++
      }
    }
  }
  await Promise.all(Array.from({ length: PREFETCH_CONCURRENCY }, worker))
  if (failures > 0) throw new Error(`${failures} ${what} could not be downloaded.`)
}

registerOfflinePrefetch('patients', async (queryClient) => {
  await Promise.all([
    queryClient.fetchQuery({ queryKey: queryKeys.patientsList(), queryFn: ({ signal }) => fetchPatientsList(signal) }),
    queryClient.fetchQuery({ queryKey: queryKeys.patientsWithLatestVisit(), queryFn: ({ signal }) => fetchPatientsWithLatestVisit(signal) }),
    queryClient.fetchQuery({ queryKey: queryKeys.tags(), queryFn: ({ signal }) => fetchTags(signal) }),
  ])
  await prefetchRecentPatients(queryClient, 'patient records', id =>
    queryClient.fetchQuery({ queryKey: queryKeys.patient(id), queryFn: ({ signal }) => fetchPatient(id, signal) }))
})

// Visits (with their station journey) are clinical: not for the referral desk.
registerOfflinePrefetch('patient-visits', queryClient =>
  prefetchRecentPatients(queryClient, 'patient visit lists', id =>
    queryClient.fetchQuery({ queryKey: queryKeys.patientVisits(id), queryFn: ({ signal }) => fetchPatientVisits(id, signal) })),
{ roles: CLINIC_STAFF_ROLES })

// Staff names for the referral "PN member" picker.
registerOfflinePrefetch('staff-users', async (queryClient) => {
  await queryClient.fetchQuery({ queryKey: queryKeys.staffUsers(), queryFn: ({ signal }) => fetchStaffUsers(signal).then(x => x.users) })
})

// ── Cache edits ──────────────────────────────────────────────────────────

/** Case-insensitively unique, trimmed, sorted like the API returns them. */
export function normalizeTags(tags: readonly string[] | undefined): string[] {
  const seen = new Set<string>()
  const out: string[] = []
  for (const raw of tags ?? []) {
    const name = raw?.trim()
    if (!name) continue
    const key = name.toUpperCase()
    if (seen.has(key)) continue
    seen.add(key)
    out.push(name)
  }
  return out.sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }))
}

function compact(value: string | null | undefined) {
  const trimmed = value?.trim()
  return trimmed ? trimmed : null
}

/** Card code of a patient: the village code and the card number, or "NEW" until the server numbers it. */
export function pendingDisplayId(villageCode: string | null | undefined): string {
  return `${villageCode ?? ''}${PENDING_CARD_NUMBER}`
}

function filterStringOf(displayId: string, patient: { name: string, contactNo: string | null, identificationNumber: string | null }, tags: readonly string[]) {
  return `${displayId} ${patient.name} ${patient.identificationNumber ?? ''} ${patient.contactNo ?? ''} ${tags.join(' ')}`.toLowerCase()
}

export const blankJourney = (): PatientVisitJourney => ({
  vitals: false,
  vision: false,
  consult: false,
  dental: false,
  dentalReferred: false,
  pharmacyOrders: 0,
  pharmacyPending: 0,
  vitalsAt: null,
  visionAt: null,
  consultAt: null,
  dentalAt: null,
  pharmacyAt: null,
})

function sortVisits(visits: PatientVisit[]): PatientVisit[] {
  return [...visits].sort((a, b) =>
    new Date(b.date).getTime() - new Date(a.date).getTime() || b.id.localeCompare(a.id))
}

/**
 * Puts `visit` into the patient's cached visits (newest first). When the
 * patient's visits are not cached, `create` starts the list with this visit.
 */
function upsertVisit(queryClient: QueryClient, visit: PatientVisit, create = false) {
  queryClient.setQueryData<PatientVisits | undefined>(queryKeys.patientVisits(visit.patientId), (old) => {
    if (!old && !create) return old
    const others = (old?.visits ?? []).filter(v => v.id !== visit.id)
    const existing = old?.visits.find(v => v.id === visit.id)
    // A visit the server already counted keeps its journey.
    return { ...old, visits: sortVisits([{ ...visit, journey: existing?.journey ?? visit.journey }, ...others]) }
  })
}

type PatientRow = PatientListItem | PatientWithLatestVisitItem | PatientDetail

/** Merges `patch` into the patient `id` in the detail, list and station worklist caches. */
function patchPatient(queryClient: QueryClient, id: string, patch: (row: PatientRow) => Partial<PatientRow>) {
  queryClient.setQueryData<PatientDetail | undefined>(queryKeys.patient(id), old => old && { ...old, ...patch(old) } as PatientDetail)
  queryClient.setQueryData<PatientsList | undefined>(queryKeys.patientsList(), old =>
    old && { ...old, patients: old.patients.map(p => (p.id === id ? { ...p, ...patch(p) } as PatientListItem : p)) })
  queryClient.setQueryData<PatientsWithLatestVisit | undefined>(queryKeys.patientsWithLatestVisit(), old =>
    old && { ...old, patients: old.patients.map(p => (p.id === id ? { ...p, ...patch(p) } as PatientWithLatestVisitItem : p)) })
}

/** Adds the patient to the cached list (oldest id first) and the worklist (newest visit first), replacing the same id. */
function insertPatient(queryClient: QueryClient, detail: PatientDetail, villageColorHex: string | null, journey: PatientVisitJourney | null) {
  queryClient.setQueryData<PatientDetail>(queryKeys.patient(detail.id), detail)
  queryClient.setQueryData<PatientsList | undefined>(queryKeys.patientsList(), (old) => {
    if (!old) return old
    const item: PatientListItem = { ...detail }
    const exists = old.patients.some(p => p.id === detail.id)
    return { ...old, patients: exists ? old.patients.map(p => (p.id === detail.id ? item : p)) : [...old.patients, item] }
  })
  queryClient.setQueryData<PatientsWithLatestVisit | undefined>(queryKeys.patientsWithLatestVisit(), (old) => {
    if (!old) return old
    const item: PatientWithLatestVisitItem = { ...detail, villageColorHex, lastVisitJourney: journey }
    return { ...old, patients: [item, ...old.patients.filter(p => p.id !== detail.id)] }
  })
}

/** Moves the patient to the front of the worklist with `visit` as their latest visit. */
function setLatestVisit(queryClient: QueryClient, patientId: string, visit: PatientVisit) {
  const latest = { lastVisitId: visit.id, lastVisitDate: visit.date }
  queryClient.setQueryData<PatientDetail | undefined>(queryKeys.patient(patientId), old => old && { ...old, ...latest })
  queryClient.setQueryData<PatientsList | undefined>(queryKeys.patientsList(), old =>
    old && { ...old, patients: old.patients.map(p => (p.id === patientId ? { ...p, ...latest } : p)) })
  queryClient.setQueryData<PatientsWithLatestVisit | undefined>(queryKeys.patientsWithLatestVisit(), (old) => {
    const current = old?.patients.find(p => p.id === patientId)
    if (!old || !current) return old
    // Never replace a newer latest visit (e.g. the server's) with an older one.
    if (current.lastVisitDate && new Date(current.lastVisitDate) > new Date(visit.date)) return old
    const updated: PatientWithLatestVisitItem = {
      ...current,
      ...latest,
      villageColorHex: visit.villageColorHex,
      lastVisitJourney: current.lastVisitId === visit.id ? current.lastVisitJourney : visit.journey,
    }
    return { ...old, patients: [updated, ...old.patients.filter(p => p.id !== patientId)] }
  })
}

function addTagsToCache(queryClient: QueryClient, tags: readonly string[]) {
  if (tags.length === 0) return
  queryClient.setQueryData<string[] | undefined>(queryKeys.tags(), old => old && normalizeTags([...old, ...tags]))
}

// ── Register a patient (patient + first visit) ───────────────────────────

export interface PatientFields {
  name: string
  identificationNumber: string
  contactNo: string
  gender: 'male' | 'female'
  /** ISO date-time. */
  dateOfBirth: string
  drugAllergy: string
  hasPoorCard: boolean
  hasBS2Card: boolean
  hasSabaiCard: boolean
  tags: string[]
}

/** The village a visit is recorded in; kept in the vars so the optimistic copy needs no lookup. */
export interface VisitVillage {
  villageCodeId: string
  code: string
  name: string
  colorHex: string
}

export interface RegisterPatientVars extends PatientFields {
  /** Client-chosen ids, sent so a replayed registration is recognised. */
  id: string
  visitId: string
  vitalsId?: string
  village: VisitVillage
  vitals?: SaBaiAPIEndpointsPatientsCreatePostVitalsRequest
  /** ISO time the registration was made (the optimistic visit date). */
  registeredAt: string
}

export function newRegisterPatientVars(fields: PatientFields, village: VisitVillage, vitals?: SaBaiAPIEndpointsPatientsCreatePostVitalsRequest): RegisterPatientVars {
  return {
    ...fields,
    id: uuidv7(),
    visitId: uuidv7(),
    vitalsId: vitals ? uuidv7() : undefined,
    village,
    vitals,
    registeredAt: new Date().toISOString(),
  }
}

function optimisticPatient(vars: RegisterPatientVars): { detail: PatientDetail, visit: PatientVisit } {
  const tags = normalizeTags(vars.tags)
  const identificationNumber = compact(vars.identificationNumber)
  const contactNo = compact(vars.contactNo)
  const name = vars.name.trim()
  const displayId = pendingDisplayId(vars.village.code)
  const detail: PatientDetail = {
    id: vars.id,
    name,
    identificationNumber,
    contactNo,
    gender: vars.gender,
    dateOfBirth: vars.dateOfBirth,
    drugAllergy: vars.drugAllergy.trim(),
    hasPoorCard: vars.hasPoorCard,
    hasBS2Card: vars.hasBS2Card,
    hasSabaiCard: vars.hasSabaiCard,
    toGetReport: false,
    villageCodeId: vars.village.villageCodeId,
    villageCode: vars.village.code,
    displayId,
    filterString: filterStringOf(displayId, { name, contactNo, identificationNumber }, tags),
    lastVisitDate: vars.registeredAt,
    lastVisitId: vars.visitId,
    hasPhoto: false,
    hasFace: false,
    tags,
  }
  const visit: PatientVisit = {
    id: vars.visitId,
    date: vars.registeredAt,
    patientId: vars.id,
    patientName: name,
    villageCodeId: vars.village.villageCodeId,
    villageCode: vars.village.code,
    villageName: vars.village.name,
    villageColorHex: vars.village.colorHex,
    status: 'started',
    journey: blankJourney(),
  }
  return { detail, visit }
}

function applyRegistration(vars: RegisterPatientVars, queryClient: QueryClient) {
  const { detail, visit } = optimisticPatient(vars)
  insertPatient(queryClient, detail, visit.villageColorHex, visit.journey)
  upsertVisit(queryClient, visit, true)
  addTagsToCache(queryClient, detail.tags ?? [])
}

/** The server's numbered patient replaces the optimistic one (it keeps the same id). */
function applyRegistered(queryClient: QueryClient, response: SaBaiAPIEndpointsPatientsCreatePostResponse) {
  const { visitId: _visitId, vitalsId: _vitalsId, ...patient } = response
  patchPatient(queryClient, response.id, () => patient)
}

export const registerPatient = defineOfflineMutation({
  key: 'patients.register',
  describe: (vars: RegisterPatientVars) => `Register patient ${vars.name.trim()}`,
  recordIds: vars => [vars.id, vars.visitId, ...(vars.vitalsId ? [vars.vitalsId] : [])],
  send: (vars, ctx) =>
    saBaiAPIEndpointsPatientsCreatePostEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      body: {
        id: vars.id,
        visitId: vars.visitId,
        vitalsId: vars.vitalsId,
        name: vars.name,
        identificationNumber: vars.identificationNumber,
        contactNo: vars.contactNo,
        gender: vars.gender,
        dateOfBirth: vars.dateOfBirth,
        drugAllergy: vars.drugAllergy,
        hasPoorCard: vars.hasPoorCard,
        hasBS2Card: vars.hasBS2Card,
        hasSabaiCard: vars.hasSabaiCard,
        toGetReport: false,
        villageCodeId: vars.village.villageCodeId,
        tags: vars.tags,
        vitals: vars.vitals,
      },
    }).unwrap(),
  optimistic: applyRegistration,
  onSuccess: async (result, _vars, queryClient) => {
    applyRegistered(queryClient, result)
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.patients() }),
      queryClient.invalidateQueries({ queryKey: queryKeys.tags() }),
    ])
  },
})

// ── Edit a patient ───────────────────────────────────────────────────────

export interface UpdatePatientVars extends PatientFields {
  id: string
}

function applyUpdate(vars: UpdatePatientVars, queryClient: QueryClient) {
  const tags = normalizeTags(vars.tags)
  patchPatient(queryClient, vars.id, (row) => {
    const name = vars.name.trim()
    const identificationNumber = compact(vars.identificationNumber)
    const contactNo = compact(vars.contactNo)
    return {
      name,
      identificationNumber,
      contactNo,
      gender: vars.gender,
      dateOfBirth: vars.dateOfBirth,
      drugAllergy: vars.drugAllergy.trim() || 'None',
      hasPoorCard: vars.hasPoorCard,
      hasBS2Card: vars.hasBS2Card,
      hasSabaiCard: vars.hasSabaiCard,
      tags,
      filterString: filterStringOf(row.displayId, { name, contactNo, identificationNumber }, tags),
    }
  })
  // The name is repeated on each of the patient's visits.
  queryClient.setQueryData<PatientVisits | undefined>(queryKeys.patientVisits(vars.id), old =>
    old && { ...old, visits: old.visits.map(v => ({ ...v, patientName: vars.name.trim() })) })
  addTagsToCache(queryClient, tags)
}

export const updatePatient = defineOfflineMutation({
  key: 'patients.update',
  describe: (vars: UpdatePatientVars) => `Edit patient ${vars.name.trim()}`,
  recordIds: vars => [vars.id],
  send: (vars, ctx) =>
    saBaiAPIEndpointsPatientsUpdatePatchEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      path: { id: vars.id },
      body: {
        name: vars.name,
        identificationNumber: vars.identificationNumber,
        contactNo: vars.contactNo,
        gender: vars.gender,
        dateOfBirth: vars.dateOfBirth,
        drugAllergy: vars.drugAllergy,
        hasPoorCard: vars.hasPoorCard,
        hasBS2Card: vars.hasBS2Card,
        hasSabaiCard: vars.hasSabaiCard,
        tags: vars.tags,
      },
    }).unwrap(),
  optimistic: applyUpdate,
  onSuccess: async (result: SaBaiAPIEndpointsPatientsUpdatePatchResponse, vars, queryClient) => {
    patchPatient(queryClient, vars.id, () => result)
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.patients() }),
      queryClient.invalidateQueries({ queryKey: queryKeys.tags() }),
    ])
  },
})

// ── Start a visit ────────────────────────────────────────────────────────

export interface StartVisitVars {
  /** Client-chosen visit id. */
  id: string
  patientId: string
  patientName: string
  village: VisitVillage
  /** ISO time the visit was started on this device; sent so replay keeps the order of arrival. */
  date: string
}

export function newStartVisitVars(patientId: string, patientName: string, village: VisitVillage): StartVisitVars {
  return { id: uuidv7(), patientId, patientName, village, date: new Date().toISOString() }
}

function optimisticVisit(vars: StartVisitVars): PatientVisit {
  return {
    id: vars.id,
    date: vars.date,
    patientId: vars.patientId,
    patientName: vars.patientName,
    villageCodeId: vars.village.villageCodeId,
    villageCode: vars.village.code,
    villageName: vars.village.name,
    villageColorHex: vars.village.colorHex,
    status: 'started',
    journey: blankJourney(),
  }
}

export const startVisit = defineOfflineMutation({
  key: 'visits.start',
  describe: (vars: StartVisitVars) => `New visit for ${vars.patientName}`,
  recordIds: vars => [vars.id, vars.patientId],
  send: (vars, ctx) =>
    saBaiAPIEndpointsVisitsCreatePostEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      body: { id: vars.id, patientId: vars.patientId, villageCodeId: vars.village.villageCodeId, date: vars.date },
    }).unwrap(),
  optimistic: (vars, queryClient) => {
    const visit = optimisticVisit(vars)
    upsertVisit(queryClient, visit, true)
    setLatestVisit(queryClient, vars.patientId, visit)
  },
  onSuccess: async (_result: SaBaiAPIEndpointsVisitsCreatePostResponse, _vars, queryClient) => {
    await queryClient.invalidateQueries({ queryKey: queryKeys.patients() })
  },
})

// ── Consents and disclosures ─────────────────────────────────────────────

export interface RecordConsentVars {
  id: string
  patientId: string
  consentType: SaBaiAPIDomainConsentTypeKey
  granted: boolean
  notes: string | null
  recordedByUserId: string
  /** ISO time it was recorded on this device. */
  recordedAt: string
}

export interface RecordDisclosureVars {
  id: string
  patientId: string
  recipient: string
  purpose: string
  description: string | null
  disclosedByUserId: string
  disclosedAt: string
}

export const consentsKey = (patientId: string) => ['patients', 'consents', patientId] as const
export const disclosuresKey = (patientId: string) => ['patients', 'disclosures', patientId] as const

/**
 * Puts `item` first in a cached list, replacing the same id. A list that is not
 * cached yet starts with just this item, so the record shows straight away.
 */
function upsertFirst<T extends { id: string | number }>(queryClient: QueryClient, key: readonly unknown[], item: T) {
  queryClient.setQueryData<T[]>(key, old => [item, ...(old ?? []).filter(x => x.id !== item.id)])
}

export const recordConsent = defineOfflineMutation({
  key: 'patients.recordConsent',
  describe: (vars: RecordConsentVars) => `${vars.granted ? 'Consent given' : 'Consent revoked'} (${vars.consentType})`,
  recordIds: vars => [vars.id, vars.patientId],
  send: (vars, ctx) =>
    saBaiAPIEndpointsPatientsConsentsPostEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      path: { patientId: vars.patientId },
      body: { id: vars.id, consentType: vars.consentType, granted: vars.granted, notes: vars.notes },
    }).unwrap(),
  optimistic: (vars, queryClient) =>
    upsertFirst(queryClient, consentsKey(vars.patientId), {
      id: vars.id,
      consentType: vars.consentType,
      granted: vars.granted,
      recordedAt: vars.recordedAt,
      revokedAt: vars.granted ? null : vars.recordedAt,
      recordedByUserId: vars.recordedByUserId,
      notes: vars.notes,
    }),
  onSuccess: (_result, vars, queryClient) => queryClient.invalidateQueries({ queryKey: consentsKey(vars.patientId) }),
})

export const recordDisclosure = defineOfflineMutation({
  key: 'patients.recordDisclosure',
  describe: (vars: RecordDisclosureVars) => `Disclosure to ${vars.recipient}`,
  recordIds: vars => [vars.id, vars.patientId],
  send: (vars, ctx) =>
    saBaiAPIEndpointsPatientsDisclosuresPostEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      path: { patientId: vars.patientId },
      body: { id: vars.id, recipient: vars.recipient, purpose: vars.purpose, description: vars.description },
    }).unwrap(),
  optimistic: (vars, queryClient) =>
    upsertFirst(queryClient, disclosuresKey(vars.patientId), {
      id: vars.id,
      recipient: vars.recipient,
      purpose: vars.purpose,
      description: vars.description,
      disclosedAt: vars.disclosedAt,
      disclosedByUserId: vars.disclosedByUserId,
    }),
  onSuccess: (_result, vars, queryClient) => queryClient.invalidateQueries({ queryKey: disclosuresKey(vars.patientId) }),
})

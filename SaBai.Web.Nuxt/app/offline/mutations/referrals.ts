import type { QueryClient } from '@tanstack/vue-query'
import {
  saBaiAPIEndpointsAuthUsersListGetEndpoint,
  saBaiAPIEndpointsReferralPartnersCreatePostEndpoint,
  saBaiAPIEndpointsReferralPartnersDeleteDeleteEndpoint,
  saBaiAPIEndpointsReferralPartnersListGetEndpoint,
  saBaiAPIEndpointsReferralPartnersUpdatePutEndpoint,
  saBaiAPIEndpointsReferralsCreatePostEndpoint,
  saBaiAPIEndpointsReferralsGetEndpoint,
  saBaiAPIEndpointsReferralsListByConsultGetEndpoint,
  saBaiAPIEndpointsReferralsListGetEndpoint,
  saBaiAPIEndpointsReferralsUpdateBoothPatchEndpoint,
  saBaiAPIEndpointsReferralsUpdateMoneyPatchEndpoint,
  saBaiAPIEndpointsReferralsUpdateOutcomePatchEndpoint,
  saBaiAPIEndpointsReferralsUpdatePartyPatchEndpoint,
  saBaiAPIEndpointsReferralsUpdateStatePatchEndpoint,
} from '@sabai/sdk/clients'
import type {
  SaBaiAPIDomainReferralStateKey,
  SaBaiAPIEndpointsReferralPartnersListGetResponse,
  SaBaiAPIEndpointsReferralsGetResponse,
  SaBaiAPIEndpointsReferralsListByConsultGetResponse,
  SaBaiAPIEndpointsReferralsListGetResponse,
} from '@sabai/sdk/types'
import { defineOfflineMutation, requestOptions } from '~/offline/defineOfflineMutation'
import { registerOfflinePrefetch } from '~/offline/prefetch'
import { apiConfig, queryKeys } from '~/utils/api'
import { getApiErrorStatus } from '~/utils/apiErrors'
import type { GuardiansContacted, ReferralServiceClass } from '~/utils/referral'

/**
 * Referrals and referral partners: reads, offline-capable writes and the
 * offline download. Cached shapes:
 * - `queryKeys.referrals()`: the list (newest first),
 * - `queryKeys.referral(id)`: one referral with its booth, party and money parts,
 * - `queryKeys.referralsByConsult(consultId)`: the referrals of one consult,
 * - `queryKeys.referralPartners()`: partners, active first.
 */

export type ReferralDetail = SaBaiAPIEndpointsReferralsGetResponse
export type ReferralListEntry = SaBaiAPIEndpointsReferralsListGetResponse
export type ConsultReferral = SaBaiAPIEndpointsReferralsListByConsultGetResponse
export type ReferralPartner = SaBaiAPIEndpointsReferralPartnersListGetResponse

// ── Reads ────────────────────────────────────────────────────────────────

export function fetchReferrals(signal?: AbortSignal) {
  return saBaiAPIEndpointsReferralsListGetEndpoint({ ...apiConfig, signal }).unwrap()
}

export function fetchReferral(id: string, signal?: AbortSignal) {
  return saBaiAPIEndpointsReferralsGetEndpoint({ ...apiConfig, path: { id }, signal }).unwrap()
}

export function fetchReferralsByConsult(consultId: string, signal?: AbortSignal) {
  return saBaiAPIEndpointsReferralsListByConsultGetEndpoint({ ...apiConfig, path: { consultId }, signal }).unwrap()
}

/** Active partners first, then by name. */
export function sortReferralPartners(partners: readonly ReferralPartner[]): ReferralPartner[] {
  return [...partners].sort((a, b) => Number(b.isActive) - Number(a.isActive) || a.name.localeCompare(b.name))
}

export async function fetchReferralPartners(signal?: AbortSignal) {
  return sortReferralPartners(await saBaiAPIEndpointsReferralPartnersListGetEndpoint({ ...apiConfig, signal }).unwrap())
}

/** Referrals still being worked; their pages are downloaded for offline use. */
const OPEN_STATES: readonly SaBaiAPIDomainReferralStateKey[] = ['New', 'Seen', 'Outgoing']
const PREFETCH_DETAIL_CONCURRENCY = 4

registerOfflinePrefetch('referrals', async (queryClient) => {
  const [referrals] = await Promise.all([
    queryClient.fetchQuery({ queryKey: queryKeys.referrals(), queryFn: ({ signal }) => fetchReferrals(signal) }),
    queryClient.fetchQuery({ queryKey: queryKeys.referralPartners(), queryFn: ({ signal }) => fetchReferralPartners(signal) }),
    // The referral page names the PN members and staff from this list.
    queryClient.fetchQuery({
      queryKey: queryKeys.staffUsers(),
      queryFn: ({ signal }) =>
        saBaiAPIEndpointsAuthUsersListGetEndpoint({ ...apiConfig, signal }).unwrap().then(x => x.users),
    }),
  ])
  const pending = referrals.filter(r => OPEN_STATES.includes(r.referralState)).map(r => r.id)
  const worker = async () => {
    for (let id = pending.shift(); id; id = pending.shift()) {
      const referralId = id
      await queryClient.fetchQuery({ queryKey: queryKeys.referral(referralId), queryFn: ({ signal }) => fetchReferral(referralId, signal) })
    }
  }
  await Promise.all(Array.from({ length: PREFETCH_DETAIL_CONCURRENCY }, worker))
})

// ── Cache edits ──────────────────────────────────────────────────────────

/** Fields of the referral detail a write can set. */
export type ReferralPatch = Partial<Pick<ReferralDetail,
  | 'referralState' | 'referralOutcome'
  | 'referredToId' | 'memberInChargeId' | 'guardiansContacted' | 'classOfService'
  | 'descriptionOfService' | 'patientSummaryFromScreening' | 'remarksAtBooth'
  | 'memberOnRunId' | 'presentAtReferralParty' | 'referralPartyResults' | 'followUp' | 'doctorsUpdated'
  | 'amountPaidUsd' | 'amountPaidRiels' | 'memberWhoPaidId'>>

const CONSULT_REFERRALS_KEY = ['referrals', 'consult'] as const

/** Sets `patch` on the referral wherever it is cached (detail, list row, consult list). */
function patchReferral(queryClient: QueryClient, id: string, patch: ReferralPatch) {
  queryClient.setQueryData<ReferralDetail>(queryKeys.referral(id), old => old && { ...old, ...patch })

  const listPatch: Partial<Pick<ReferralListEntry, 'referralState' | 'referralOutcome'>> = {}
  if (patch.referralState !== undefined) listPatch.referralState = patch.referralState
  if (patch.referralOutcome !== undefined) listPatch.referralOutcome = patch.referralOutcome
  if (Object.keys(listPatch).length === 0) return
  queryClient.setQueryData<ReferralListEntry[]>(queryKeys.referrals(), old =>
    old?.map(r => (r.id === id ? { ...r, ...listPatch } : r)))
  queryClient.setQueriesData<ConsultReferral[]>({ queryKey: CONSULT_REFERRALS_KEY }, old =>
    old?.map(r => (r.id === id ? { ...r, ...listPatch } : r)))
}

/** The server's answer replaces the cached detail; the list is refreshed. */
function afterPatch(updated: Partial<ReferralDetail> & { id: string }, queryClient: QueryClient) {
  const { id, referralState, referralOutcome } = updated
  patchReferral(queryClient, id, { referralState, referralOutcome })
  return queryClient.invalidateQueries({ queryKey: queryKeys.referrals() })
}

function afterFullPatch(updated: ReferralDetail, _vars: unknown, queryClient: QueryClient) {
  queryClient.setQueryData<ReferralDetail>(queryKeys.referral(updated.id), updated)
  return afterPatch(updated, queryClient)
}

function upsertById<T extends { id: string }>(list: T[] | undefined, item: T, atStart = true): T[] | undefined {
  if (!list) return list
  return list.some(x => x.id === item.id)
    ? list.map(x => (x.id === item.id ? item : x))
    : atStart ? [item, ...list] : [...list, item]
}

// ── Referrals: writes ────────────────────────────────────────────────────

/** What the cached screens show about the patient and consult; lets a referral made offline appear in the list and open. */
export interface CreateReferralContext {
  patientId: string
  patientName: string
  hasPhoto: boolean
  villageCodeId: string
  villageCode: string
  villageColorHex: string
  doctorId: string
  doctorNickname: string
  consultDate: string
  patientDateOfBirth?: string
  patientContactNo?: string | null
}

export interface CreateReferralVars {
  id: string
  consultId: string
  referredFor: string
  referralNotes: string | null
  /** Optimistic display only; not sent. */
  context?: CreateReferralContext
}

function optimisticReferral(vars: CreateReferralVars, now: string) {
  const base: ConsultReferral = {
    id: vars.id,
    referredFor: vars.referredFor,
    referralNotes: vars.referralNotes,
    referralState: 'New',
    referralOutcome: null,
    consultId: vars.consultId,
    createdAt: now,
  }
  return base
}

export const createReferral = defineOfflineMutation({
  key: 'referrals.create',
  describe: (vars: CreateReferralVars) =>
    `Referral${vars.context ? ` for ${vars.context.patientName}` : ''}: ${vars.referredFor}`,
  recordIds: vars => [vars.id, vars.consultId, ...(vars.context ? [vars.context.patientId] : [])],
  send: (vars, ctx) =>
    saBaiAPIEndpointsReferralsCreatePostEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      body: {
        id: vars.id,
        consultId: vars.consultId,
        referredFor: vars.referredFor,
        referralNotes: vars.referralNotes,
      },
    }).unwrap(),
  optimistic: (vars, queryClient) => {
    const existing = queryClient.getQueryData<ConsultReferral[]>(queryKeys.referralsByConsult(vars.consultId))
      ?.find(r => r.id === vars.id)
    const created = existing ?? optimisticReferral(vars, new Date().toISOString())
    queryClient.setQueryData<ConsultReferral[]>(queryKeys.referralsByConsult(vars.consultId), old =>
      upsertById(old, created))

    const c = vars.context
    if (!c) return
    const entry: ReferralListEntry = {
      ...created,
      consultDate: c.consultDate,
      doctorId: c.doctorId,
      patientId: c.patientId,
      patientName: c.patientName,
      hasPhoto: c.hasPhoto,
      villageCodeId: c.villageCodeId,
      villageCode: c.villageCode,
      villageColorHex: c.villageColorHex,
      tags: [],
    }
    queryClient.setQueryData<ReferralListEntry[]>(queryKeys.referrals(), old => {
      const current = old?.find(r => r.id === vars.id)
      return upsertById(old, current ? { ...entry, ...current } : entry)
    })
    // Only the first write of a record fills the detail; later optimistic patches stay on top.
    queryClient.setQueryData<ReferralDetail>(queryKeys.referral(vars.id), old => old ?? {
      ...entry,
      updatedAt: created.createdAt,
      doctorNickname: c.doctorNickname,
      patientDateOfBirth: c.patientDateOfBirth ?? '',
      patientContactNo: c.patientContactNo ?? null,
      patientHasPoorCard: false,
      patientHasBS2Card: false,
      patientHasSabaiCard: false,
      referredToId: null,
      memberInChargeId: null,
      guardiansContacted: null,
      classOfService: null,
      descriptionOfService: null,
      patientSummaryFromScreening: null,
      remarksAtBooth: null,
      memberOnRunId: null,
      presentAtReferralParty: false,
      referralPartyResults: null,
      followUp: false,
      doctorsUpdated: false,
      amountPaidUsd: null,
      amountPaidRiels: null,
      memberWhoPaidId: null,
    })
  },
  onSuccess: async (_created, vars, queryClient) => {
    await queryClient.invalidateQueries({ queryKey: queryKeys.referrals() })
    await queryClient.invalidateQueries({ queryKey: queryKeys.referralsByConsult(vars.consultId) })
  },
})

/** Shown in the pending-changes list. */
interface Labelled {
  id: string
  patientName?: string
}

const forPatient = (vars: Labelled) => (vars.patientName ? ` for ${vars.patientName}` : '')

export interface UpdateReferralStateVars extends Labelled {
  referralState: SaBaiAPIDomainReferralStateKey
  /** Left as it is when omitted. */
  referralOutcome?: string | null
}

export const updateReferralState = defineOfflineMutation({
  key: 'referrals.state',
  describe: (vars: UpdateReferralStateVars) => `Referral status${forPatient(vars)}: ${vars.referralState}`,
  recordIds: vars => [vars.id],
  send: (vars, ctx) =>
    saBaiAPIEndpointsReferralsUpdateStatePatchEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      path: { id: vars.id },
      body: { referralState: vars.referralState, referralOutcome: vars.referralOutcome },
    }).unwrap(),
  optimistic: (vars, queryClient) => patchReferral(queryClient, vars.id, {
    referralState: vars.referralState,
    ...(vars.referralOutcome != null && { referralOutcome: vars.referralOutcome }),
  }),
  onSuccess: (updated, _vars, queryClient) => afterPatch(updated, queryClient),
})

export interface UpdateReferralOutcomeVars extends Labelled {
  referralOutcome: string
}

export const updateReferralOutcome = defineOfflineMutation({
  key: 'referrals.outcome',
  describe: (vars: UpdateReferralOutcomeVars) => `Referral outcome${forPatient(vars)}`,
  recordIds: vars => [vars.id],
  send: (vars, ctx) =>
    saBaiAPIEndpointsReferralsUpdateOutcomePatchEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      path: { id: vars.id },
      body: { referralOutcome: vars.referralOutcome },
    }).unwrap(),
  optimistic: (vars, queryClient) => patchReferral(queryClient, vars.id, { referralOutcome: vars.referralOutcome }),
  onSuccess: (updated, _vars, queryClient) => afterPatch(updated, queryClient),
})

export interface UpdateReferralBoothVars extends Labelled {
  referredToId: string | null
  memberInChargeId: string | null
  guardiansContacted: GuardiansContacted | null
  classOfService: ReferralServiceClass | null
  descriptionOfService: string | null
  patientSummaryFromScreening: string | null
  remarksAtBooth: string | null
}

export const updateReferralBooth = defineOfflineMutation({
  key: 'referrals.booth',
  describe: (vars: UpdateReferralBoothVars) => `Referral booth details${forPatient(vars)}`,
  // The partner may itself have been created offline.
  recordIds: vars => [vars.id, ...(vars.referredToId ? [vars.referredToId] : [])],
  send: (vars, ctx) =>
    saBaiAPIEndpointsReferralsUpdateBoothPatchEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      path: { id: vars.id },
      body: {
        referredToId: vars.referredToId,
        memberInChargeId: vars.memberInChargeId,
        guardiansContacted: vars.guardiansContacted,
        classOfService: vars.classOfService,
        descriptionOfService: vars.descriptionOfService,
        patientSummaryFromScreening: vars.patientSummaryFromScreening,
        remarksAtBooth: vars.remarksAtBooth,
      },
    }).unwrap(),
  optimistic: (vars, queryClient) => patchReferral(queryClient, vars.id, {
    referredToId: vars.referredToId,
    memberInChargeId: vars.memberInChargeId,
    guardiansContacted: vars.guardiansContacted,
    classOfService: vars.classOfService,
    descriptionOfService: vars.descriptionOfService,
    patientSummaryFromScreening: vars.patientSummaryFromScreening,
    remarksAtBooth: vars.remarksAtBooth,
  }),
  onSuccess: afterFullPatch,
})

export interface UpdateReferralPartyVars extends Labelled {
  memberOnRunId: string | null
  presentAtReferralParty: boolean
  referralPartyResults: string | null
  followUp: boolean
  doctorsUpdated: boolean
}

export const updateReferralParty = defineOfflineMutation({
  key: 'referrals.party',
  describe: (vars: UpdateReferralPartyVars) => `Referral party details${forPatient(vars)}`,
  recordIds: vars => [vars.id],
  send: (vars, ctx) =>
    saBaiAPIEndpointsReferralsUpdatePartyPatchEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      path: { id: vars.id },
      body: {
        memberOnRunId: vars.memberOnRunId,
        presentAtReferralParty: vars.presentAtReferralParty,
        referralPartyResults: vars.referralPartyResults,
        followUp: vars.followUp,
        doctorsUpdated: vars.doctorsUpdated,
      },
    }).unwrap(),
  optimistic: (vars, queryClient) => patchReferral(queryClient, vars.id, {
    memberOnRunId: vars.memberOnRunId,
    presentAtReferralParty: vars.presentAtReferralParty,
    referralPartyResults: vars.referralPartyResults,
    followUp: vars.followUp,
    doctorsUpdated: vars.doctorsUpdated,
  }),
  onSuccess: afterFullPatch,
})

export interface UpdateReferralMoneyVars extends Labelled {
  amountPaidUsd: number | null
  amountPaidRiels: number | null
  memberWhoPaidId: string | null
}

export const updateReferralMoney = defineOfflineMutation({
  key: 'referrals.money',
  describe: (vars: UpdateReferralMoneyVars) => `Referral payment${forPatient(vars)}`,
  recordIds: vars => [vars.id],
  send: (vars, ctx) =>
    saBaiAPIEndpointsReferralsUpdateMoneyPatchEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      path: { id: vars.id },
      body: {
        amountPaidUsd: vars.amountPaidUsd,
        amountPaidRiels: vars.amountPaidRiels,
        memberWhoPaidId: vars.memberWhoPaidId,
      },
    }).unwrap(),
  optimistic: (vars, queryClient) => patchReferral(queryClient, vars.id, {
    amountPaidUsd: vars.amountPaidUsd,
    amountPaidRiels: vars.amountPaidRiels,
    memberWhoPaidId: vars.memberWhoPaidId,
  }),
  onSuccess: afterFullPatch,
})

// ── Referral partners: writes ────────────────────────────────────────────

export interface SaveReferralPartnerVars {
  id: string
  name: string
  remarks: string | null
  isActive: boolean
}

export interface DeleteReferralPartnerVars {
  id: string
  /** Shown in the pending-changes list. */
  name: string
}

function upsertPartner(queryClient: QueryClient, partner: ReferralPartner) {
  queryClient.setQueryData<ReferralPartner[]>(queryKeys.referralPartners(), old =>
    old && sortReferralPartners(upsertById(old, partner)!))
}

function optimisticPartner(queryClient: QueryClient, vars: SaveReferralPartnerVars): ReferralPartner {
  const now = new Date().toISOString()
  const cached = queryClient.getQueryData<ReferralPartner[]>(queryKeys.referralPartners())?.find(p => p.id === vars.id)
  return { createdAt: now, ...cached, ...vars, updatedAt: now }
}

const afterPartnerSave = async (saved: ReferralPartner, _vars: unknown, queryClient: QueryClient) => {
  upsertPartner(queryClient, saved)
  await queryClient.invalidateQueries({ queryKey: queryKeys.referralPartners() })
}

export const createReferralPartner = defineOfflineMutation({
  key: 'referralPartners.create',
  describe: (vars: SaveReferralPartnerVars) => `New referral partner ${vars.name}`,
  recordIds: vars => [vars.id],
  send: (vars, ctx) =>
    saBaiAPIEndpointsReferralPartnersCreatePostEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      body: { id: vars.id, name: vars.name, remarks: vars.remarks, isActive: vars.isActive },
    }).unwrap(),
  optimistic: (vars, queryClient) => upsertPartner(queryClient, optimisticPartner(queryClient, vars)),
  onSuccess: afterPartnerSave,
})

export const updateReferralPartner = defineOfflineMutation({
  key: 'referralPartners.update',
  describe: (vars: SaveReferralPartnerVars) => `Edit referral partner ${vars.name}`,
  recordIds: vars => [vars.id],
  send: (vars, ctx) =>
    saBaiAPIEndpointsReferralPartnersUpdatePutEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      path: { id: vars.id },
      body: { name: vars.name, remarks: vars.remarks, isActive: vars.isActive },
    }).unwrap(),
  optimistic: (vars, queryClient) => upsertPartner(queryClient, optimisticPartner(queryClient, vars)),
  onSuccess: afterPartnerSave,
})

export const deleteReferralPartner = defineOfflineMutation({
  key: 'referralPartners.delete',
  describe: (vars: DeleteReferralPartnerVars) => `Delete referral partner ${vars.name}`,
  recordIds: vars => [vars.id],
  send: async (vars, ctx) => {
    try {
      await saBaiAPIEndpointsReferralPartnersDeleteDeleteEndpoint({ ...apiConfig, ...requestOptions(ctx), path: { id: vars.id } }).unwrap()
    }
    catch (error) {
      // Already gone (e.g. a replay of a delete whose response was lost): done.
      if (getApiErrorStatus(error) !== 404) throw error
    }
  },
  optimistic: (vars, queryClient) => {
    queryClient.setQueryData<ReferralPartner[]>(queryKeys.referralPartners(), old =>
      old?.filter(p => p.id !== vars.id))
  },
  onSuccess: (_result, _vars, queryClient) => queryClient.invalidateQueries({ queryKey: queryKeys.referralPartners() }),
})

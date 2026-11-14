import { useMutation } from '@tanstack/vue-query'
import type { SaBaiAPIDomainReferralStateKey } from '@sabai/sdk/types'
import { uuidv7 } from '~/utils/uuid'
import type { GuardiansContacted, ReferralServiceClass } from '~/utils/referral'

export interface CreateReferralInput {
  consultId: string
  referredFor: string
  referralNotes?: string | null
  referralState?: SaBaiAPIDomainReferralStateKey
}

export interface UpdateReferralStateInput {
  id: string
  referralState: SaBaiAPIDomainReferralStateKey
  referralOutcome?: string | null
}

export interface UpdateReferralOutcomeInput {
  id: string
  referralOutcome: string
}

export interface UpdateReferralBoothInput {
  id: string
  referredToId: string | null
  memberInChargeId: string | null
  guardiansContacted: GuardiansContacted | null
  classOfService: ReferralServiceClass | null
  descriptionOfService: string | null
  patientSummaryFromScreening: string | null
  remarksAtBooth: string | null
}

export interface UpdateReferralPartyInput {
  id: string
  memberOnRunId: string | null
  presentAtReferralParty: boolean
  referralPartyResults: string | null
  followUp: boolean
  doctorsUpdated: boolean
}

export interface UpdateReferralMoneyInput {
  id: string
  amountPaidUsd: number | null
  amountPaidRiels: number | null
  memberWhoPaidId: string | null
}

export interface LocalReferralItem {
  id: string
  consultId: string
  visitId: string
  patientId: string
  patientName: string
  patientNumber: number
  patientDisplayId: string
  patientDateOfBirth: string | null
  patientContactNo: string | null
  patientHasPoorCard: boolean
  patientHasBS2Card: boolean
  patientHasSabaiCard: boolean
  villageCodeId: string | null
  villageCode: string | null
  villageColorHex?: string
  hasPhoto: boolean
  referredFor: string
  referralNotes: string | null
  referralState: SaBaiAPIDomainReferralStateKey
  referralOutcome: string | null
  createdAt: string
  updatedAt: string
  consultDate: string
  doctorId: string | null
  doctorName: string | null
  doctorNickname: string | null
  referredToId: string | null
  memberInChargeId: string | null
  guardiansContacted: GuardiansContacted | null
  classOfService: ReferralServiceClass | null
  descriptionOfService: string | null
  patientSummaryFromScreening: string | null
  remarksAtBooth: string | null
  memberOnRunId: string | null
  presentAtReferralParty: boolean
  referralPartyResults: string | null
  followUp: boolean
  doctorsUpdated: boolean
  amountPaidUsd: number | null
  amountPaidRiels: number | null
  memberWhoPaidId: string | null
  tags?: string[]
}

interface RawReferralRow {
  id: string
  consult_id: string
  visit_id: string
  patient_id: string
  patient_name: string
  patient_number: number | null
  patient_dob: string | null
  patient_contact_no: string | null
  patient_has_poor_card: number | null
  patient_has_bs2_card: number | null
  patient_has_sabai_card: number | null
  village_code_id: string | null
  village_code: string | null
  village_color_hex: string | null
  patient_image_public_id: string | null
  referred_for: string
  referral_notes: string | null
  referral_state: SaBaiAPIDomainReferralStateKey
  referral_outcome: string | null
  created_at: string
  updated_at: string | null
  consult_date: string
  doctor_id: string | null
  doctor_name: string | null
  referred_to_id: string | null
  member_in_charge_id: string | null
  guardians_contacted: GuardiansContacted | null
  class_of_service: ReferralServiceClass | null
  description_of_service: string | null
  patient_summary_from_screening: string | null
  remarks_at_booth: string | null
  member_on_run_id: string | null
  present_at_referral_party: number | null
  referral_party_results: string | null
  follow_up: number | null
  doctors_updated: number | null
  amount_paid_usd: number | null
  amount_paid_riels: number | null
  member_who_paid_id: string | null
}

function mapReferralRow(r: RawReferralRow, tags: string[] = []): LocalReferralItem {
  const num = Number(r.patient_number ?? 0)
  const displayId = num > 0
    ? `${r.village_code ?? ''}${String(num).padStart(4, '0')}`
    : `${r.village_code ?? ''}NEW`
  return {
    id: r.id,
    consultId: r.consult_id,
    visitId: r.visit_id,
    patientId: r.patient_id,
    patientName: r.patient_name,
    patientNumber: num,
    patientDisplayId: displayId,
    patientDateOfBirth: r.patient_dob,
    patientContactNo: r.patient_contact_no,
    patientHasPoorCard: Boolean(r.patient_has_poor_card),
    patientHasBS2Card: Boolean(r.patient_has_bs2_card),
    patientHasSabaiCard: Boolean(r.patient_has_sabai_card),
    villageCodeId: r.village_code_id,
    villageCode: r.village_code,
    villageColorHex: r.village_color_hex ?? undefined,
    hasPhoto: Boolean(r.patient_image_public_id),
    referredFor: r.referred_for,
    referralNotes: r.referral_notes,
    referralState: r.referral_state ?? 'New',
    referralOutcome: r.referral_outcome,
    createdAt: r.created_at,
    updatedAt: r.updated_at ?? r.created_at,
    consultDate: r.consult_date,
    doctorId: r.doctor_id,
    doctorName: r.doctor_name,
    doctorNickname: r.doctor_name,
    referredToId: r.referred_to_id,
    memberInChargeId: r.member_in_charge_id,
    guardiansContacted: r.guardians_contacted,
    classOfService: r.class_of_service,
    descriptionOfService: r.description_of_service,
    patientSummaryFromScreening: r.patient_summary_from_screening,
    remarksAtBooth: r.remarks_at_booth,
    memberOnRunId: r.member_on_run_id,
    presentAtReferralParty: Boolean(r.present_at_referral_party),
    referralPartyResults: r.referral_party_results,
    followUp: Boolean(r.follow_up),
    doctorsUpdated: Boolean(r.doctors_updated),
    amountPaidUsd: r.amount_paid_usd === null || r.amount_paid_usd === undefined ? null : Number(r.amount_paid_usd),
    amountPaidRiels: r.amount_paid_riels === null || r.amount_paid_riels === undefined ? null : Number(r.amount_paid_riels),
    memberWhoPaidId: r.member_who_paid_id,
    tags,
  }
}

const REFERRAL_SELECT = `
  SELECT
    r.id,
    r.consult_id,
    c.visit_id,
    v.patient_id,
    p.name AS patient_name,
    p.number AS patient_number,
    p.date_of_birth AS patient_dob,
    p.contact_no AS patient_contact_no,
    p.has_poor_card AS patient_has_poor_card,
    p.has_bs2_card AS patient_has_bs2_card,
    p.has_sabai_card AS patient_has_sabai_card,
    p.village_code_id,
    p.patient_image_public_id,
    vc.code AS village_code,
    vc.color_hex AS village_color_hex,
    r.referred_for,
    r.referral_notes,
    r.referral_state,
    r.referral_outcome,
    r.created_at,
    r.updated_at,
    c.date AS consult_date,
    c.doctor_id,
    su.nickname AS doctor_name,
    r.referred_to_id,
    r.member_in_charge_id,
    r.guardians_contacted,
    r.class_of_service,
    r.description_of_service,
    r.patient_summary_from_screening,
    r.remarks_at_booth,
    r.member_on_run_id,
    r.present_at_referral_party,
    r.referral_party_results,
    r.follow_up,
    r.doctors_updated,
    r.amount_paid_usd,
    r.amount_paid_riels,
    r.member_who_paid_id
  FROM referrals r
  JOIN consults c ON c.id = r.consult_id
  JOIN visits v ON v.id = c.visit_id
  JOIN patients p ON p.id = v.patient_id
  LEFT JOIN village_codes vc ON vc.id = p.village_code_id
  LEFT JOIN staff_users su ON su.id = c.doctor_id
`

export function useReferralsList() {
  return useLocalQuery<LocalReferralItem[]>(
    ['referrals', 'consults', 'visits', 'patients', 'village_codes', 'staff_users', 'patient_tags', 'tags'],
    async () => {
      const db = getPowerSyncInstance()
      const [rows, tagRows] = await Promise.all([
        db.getAll<RawReferralRow>(`${REFERRAL_SELECT} ORDER BY r.created_at DESC`),
        db.getAll<{ patient_id: string, name: string | null }>(
          `SELECT pt.patient_id, COALESCE(t.name, pt.tag_name) AS name
           FROM patient_tags pt
           LEFT JOIN tags t ON t.id = pt.tag_id
           ORDER BY name ASC`,
        ),
      ])
      const tagsByPatient = new Map<string, string[]>()
      for (const tr of tagRows) {
        if (!tr.name) continue
        const list = tagsByPatient.get(tr.patient_id) ?? []
        if (!list.includes(tr.name)) list.push(tr.name)
        tagsByPatient.set(tr.patient_id, list)
      }
      return rows.map(r => mapReferralRow(r, tagsByPatient.get(r.patient_id) ?? []))
    },
  )
}

export function useReferral(id: MaybeRefOrGetter<string | null | undefined>) {
  return useLocalQuery<LocalReferralItem | null>(
    ['referrals', 'consults', 'visits', 'patients', 'village_codes', 'staff_users'],
    async () => {
      const rid = toValue(id)
      if (!rid) return null
      const db = getPowerSyncInstance()
      const row = await db.getOptional<RawReferralRow>(
        `${REFERRAL_SELECT} WHERE r.id = ?`,
        [rid],
      )
      return row ? mapReferralRow(row) : null
    },
    {
      enabled: computed(() => Boolean(toValue(id))),
      watchDeps: [() => toValue(id)],
    },
  )
}

export function useConsultReferrals(consultId: MaybeRefOrGetter<string | null | undefined>) {
  return useLocalQuery<LocalReferralItem[]>(
    ['referrals', 'consults', 'visits', 'patients', 'village_codes', 'staff_users'],
    async () => {
      const cid = toValue(consultId)
      if (!cid) return []
      const db = getPowerSyncInstance()
      const rows = await db.getAll<RawReferralRow>(
        `${REFERRAL_SELECT} WHERE r.consult_id = ? ORDER BY r.created_at DESC`,
        [cid],
      )
      return rows.map(r => mapReferralRow(r))
    },
    {
      enabled: computed(() => Boolean(toValue(consultId))),
      watchDeps: [() => toValue(consultId)],
    },
  )
}

export function useCreateReferral() {
  return useMutation({
    mutationFn: async (input: CreateReferralInput) => {
      const db = getPowerSyncInstance()
      const id = uuidv7()
      const now = new Date().toISOString()
      await db.execute(
        `INSERT INTO referrals (id, version, updated_at, consult_id, referred_for, referral_notes, referral_state, created_at)
         VALUES (?, 1, ?, ?, ?, ?, ?, ?)`,
        [
          id,
          now,
          input.consultId,
          input.referredFor,
          input.referralNotes ?? null,
          input.referralState ?? 'New',
          now,
        ],
      )
      return { id }
    },
  })
}

export function useUpdateReferralState() {
  return useMutation({
    mutationFn: async (input: UpdateReferralStateInput) => {
      const db = getPowerSyncInstance()
      const now = new Date().toISOString()
      if (input.referralOutcome !== undefined) {
        await db.execute(
          'UPDATE referrals SET referral_state = ?, referral_outcome = ?, updated_at = ? WHERE id = ?',
          [input.referralState, input.referralOutcome, now, input.id],
        )
      }
      else {
        await db.execute(
          'UPDATE referrals SET referral_state = ?, updated_at = ? WHERE id = ?',
          [input.referralState, now, input.id],
        )
      }
      return { id: input.id, referralState: input.referralState }
    },
  })
}

export function useUpdateReferralOutcome() {
  return useMutation({
    mutationFn: async (input: UpdateReferralOutcomeInput) => {
      const db = getPowerSyncInstance()
      const now = new Date().toISOString()
      await db.execute(
        'UPDATE referrals SET referral_outcome = ?, updated_at = ? WHERE id = ?',
        [input.referralOutcome, now, input.id],
      )
      return { id: input.id, referralOutcome: input.referralOutcome }
    },
  })
}

/**
 * Updates only the referral booth fields so saving booth details cannot overwrite
 * concurrent edits to party or money sections.
 */
export function useUpdateReferralBooth() {
  return useMutation({
    mutationFn: async (input: UpdateReferralBoothInput) => {
      const db = getPowerSyncInstance()
      const now = new Date().toISOString()
      await db.execute(
        `UPDATE referrals
         SET referred_to_id = ?,
             member_in_charge_id = ?,
             guardians_contacted = ?,
             class_of_service = ?,
             description_of_service = ?,
             patient_summary_from_screening = ?,
             remarks_at_booth = ?,
             updated_at = ?
         WHERE id = ?`,
        [
          input.referredToId,
          input.memberInChargeId,
          input.guardiansContacted,
          input.classOfService,
          input.descriptionOfService?.trim() || null,
          input.patientSummaryFromScreening?.trim() || null,
          input.remarksAtBooth?.trim() || null,
          now,
          input.id,
        ],
      )
      return input
    },
  })
}

/**
 * Updates only the referral party fields.
 */
export function useUpdateReferralParty() {
  return useMutation({
    mutationFn: async (input: UpdateReferralPartyInput) => {
      const db = getPowerSyncInstance()
      const now = new Date().toISOString()
      await db.execute(
        `UPDATE referrals
         SET member_on_run_id = ?,
             present_at_referral_party = ?,
             referral_party_results = ?,
             follow_up = ?,
             doctors_updated = ?,
             updated_at = ?
         WHERE id = ?`,
        [
          input.memberOnRunId,
          input.presentAtReferralParty ? 1 : 0,
          input.referralPartyResults?.trim() || null,
          input.followUp ? 1 : 0,
          input.doctorsUpdated ? 1 : 0,
          now,
          input.id,
        ],
      )
      return input
    },
  })
}

/**
 * Updates only the referral money fields.
 */
export function useUpdateReferralMoney() {
  return useMutation({
    mutationFn: async (input: UpdateReferralMoneyInput) => {
      const db = getPowerSyncInstance()
      const now = new Date().toISOString()
      await db.execute(
        `UPDATE referrals
         SET amount_paid_usd = ?,
             amount_paid_riels = ?,
             member_who_paid_id = ?,
             updated_at = ?
         WHERE id = ?`,
        [
          input.amountPaidUsd,
          input.amountPaidRiels,
          input.memberWhoPaidId,
          now,
          input.id,
        ],
      )
      return input
    },
  })
}

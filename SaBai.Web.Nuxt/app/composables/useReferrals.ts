import type { MaybeRefOrGetter } from 'vue'
import type { SaBaiAPIDomainReferralStateKey } from '@sabai/sdk/types'
import { uuidv7 } from '~/utils/uuid'

export interface CreateReferralInput {
  consultId: string
  referredFor: string
  referralNotes?: string
}

export interface UpdateReferralStateInput {
  id: string
  referralState: SaBaiAPIDomainReferralStateKey
  referralOutcome?: string
}

export interface UpdateReferralOutcomeInput {
  id: string
  referralOutcome: string
}

export interface LocalReferralItem {
  id: string
  consultId: string
  visitId: string
  patientId: string
  patientName: string
  patientNumber: number
  patientDisplayId: string
  villageCodeId: string | null
  villageCode: string | null
  villageColorHex?: string
  hasPhoto: boolean
  referredFor: string
  referralNotes: string | null
  referralState: SaBaiAPIDomainReferralStateKey
  referralOutcome: string | null
  createdAt: string
  consultDate: string
  doctorName: string | null
  doctorNickname: string | null
}

function mapReferralRow(r: {
  id: string
  consult_id: string
  visit_id: string
  patient_id: string
  patient_name: string
  patient_number: number | null
  village_code_id: string | null
  village_code: string | null
  village_color_hex: string | null
  patient_image_public_id: string | null
  referred_for: string
  referral_notes: string | null
  referral_state: SaBaiAPIDomainReferralStateKey
  referral_outcome: string | null
  created_at: string
  consult_date: string
  doctor_name: string | null
}): LocalReferralItem {
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
    villageCodeId: r.village_code_id,
    villageCode: r.village_code,
    villageColorHex: r.village_color_hex ?? undefined,
    hasPhoto: Boolean(r.patient_image_public_id),
    referredFor: r.referred_for,
    referralNotes: r.referral_notes,
    referralState: r.referral_state ?? 'New',
    referralOutcome: r.referral_outcome,
    createdAt: r.created_at,
    consultDate: r.consult_date,
    doctorName: r.doctor_name,
    doctorNickname: r.doctor_name,
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
    p.village_code_id,
    p.patient_image_public_id,
    vc.code AS village_code,
    vc.color_hex AS village_color_hex,
    r.referred_for,
    r.referral_notes,
    r.referral_state,
    r.referral_outcome,
    r.created_at,
    c.date AS consult_date,
    su.nickname AS doctor_name
  FROM referrals r
  JOIN consults c ON c.id = r.consult_id
  JOIN visits v ON v.id = c.visit_id
  JOIN patients p ON p.id = v.patient_id
  LEFT JOIN village_codes vc ON vc.id = p.village_code_id
  LEFT JOIN staff_users su ON su.id = c.doctor_id
`

export function useReferralsList() {
  return useLocalQuery<LocalReferralItem[]>(
    ['referrals', 'consults', 'visits', 'patients', 'village_codes', 'staff_users'],
    async () => {
      const db = getPowerSyncInstance()
      const rows = await db.getAll<Parameters<typeof mapReferralRow>[0]>(
        `${REFERRAL_SELECT} ORDER BY r.created_at DESC`,
      )
      return rows.map(mapReferralRow)
    },
  )
}

export function useReferral(id: MaybeRefOrGetter<string | null | undefined>) {
  return useLocalQuery<LocalReferralItem | null>(
    ['referrals', 'consults', 'visits', 'patients', 'village_codes', 'staff_users'],
    async () => {
      const refId = toValue(id)
      if (!refId) return null
      const db = getPowerSyncInstance()
      const row = await db.getOptional<Parameters<typeof mapReferralRow>[0]>(
        `${REFERRAL_SELECT} WHERE r.id = ?`,
        [refId],
      )
      return row ? mapReferralRow(row) : null
    },
    {
      enabled: computed(() => Boolean(toValue(id))),
      watchDeps: [() => toValue(id)],
    },
  )
}

export function useReferralsByConsult(consultId: MaybeRefOrGetter<string | null | undefined>) {
  return useLocalQuery<{ referrals: LocalReferralItem[] }>(
    ['referrals', 'consults', 'visits', 'patients', 'village_codes', 'staff_users'],
    async () => {
      const cid = toValue(consultId)
      if (!cid) return { referrals: [] }
      const db = getPowerSyncInstance()
      const rows = await db.getAll<Parameters<typeof mapReferralRow>[0]>(
        `${REFERRAL_SELECT} WHERE r.consult_id = ? ORDER BY r.created_at DESC`,
        [cid],
      )
      return { referrals: rows.map(mapReferralRow) }
    },
    {
      enabled: computed(() => Boolean(toValue(consultId))),
      watchDeps: [() => toValue(consultId)],
    },
  )
}

interface MutationCallbacks<TResult> {
  onSuccess?: (result: TResult) => void
  onError?: (err: Error) => void
}

function useLocalMutation<TInput, TResult>(fn: (input: TInput) => Promise<TResult>) {
  const isPending = ref(false)
  async function mutateAsync(input: TInput): Promise<TResult> {
    isPending.value = true
    try {
      return await fn(input)
    } finally {
      isPending.value = false
    }
  }
  function mutate(input: TInput, callbacks?: MutationCallbacks<TResult>) {
    void mutateAsync(input)
      .then(res => callbacks?.onSuccess?.(res))
      .catch(err => callbacks?.onError?.(err instanceof Error ? err : new Error(String(err))))
  }
  return {
    isPending: readonly(isPending),
    mutateAsync,
    mutate,
  }
}

export function useCreateReferral() {
  return useLocalMutation(async (input: CreateReferralInput) => {
    const db = getPowerSyncInstance()
    const id = uuidv7()
    const now = new Date().toISOString()
    await db.execute(
      `INSERT INTO referrals (id, version, updated_at, consult_id, referred_for, referral_notes, referral_state, created_at)
       VALUES (?, 1, ?, ?, ?, ?, 'New', ?)`,
      [id, now, input.consultId, input.referredFor.trim(), input.referralNotes?.trim() || null, now],
    )
    await refreshPendingCrudCount()
    return { id }
  })
}

export function useUpdateReferralState() {
  return useLocalMutation(async (input: UpdateReferralStateInput) => {
    const db = getPowerSyncInstance()
    const now = new Date().toISOString()
    if (input.referralOutcome !== undefined) {
      await db.execute(
        `UPDATE referrals SET referral_state = ?, referral_outcome = ?, updated_at = ? WHERE id = ?`,
        [input.referralState, input.referralOutcome, now, input.id],
      )
    } else {
      await db.execute(
        `UPDATE referrals SET referral_state = ?, updated_at = ? WHERE id = ?`,
        [input.referralState, now, input.id],
      )
    }
    await refreshPendingCrudCount()
  })
}

export function useUpdateReferralOutcome() {
  return useLocalMutation(async (input: UpdateReferralOutcomeInput) => {
    const db = getPowerSyncInstance()
    const now = new Date().toISOString()
    await db.execute(
      `UPDATE referrals SET referral_outcome = ?, updated_at = ? WHERE id = ?`,
      [input.referralOutcome, now, input.id],
    )
    await refreshPendingCrudCount()
  })
}

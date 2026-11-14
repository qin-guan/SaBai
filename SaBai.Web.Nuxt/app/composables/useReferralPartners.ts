import { useMutation } from '@tanstack/vue-query'
import { uuidv7 } from '~/utils/uuid'

export interface ReferralPartner {
  id: string
  name: string
  remarks: string | null
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateReferralPartnerInput {
  name: string
  remarks?: string | null
  isActive?: boolean
}

export interface UpdateReferralPartnerInput {
  id: string
  name: string
  remarks?: string | null
  isActive: boolean
}

interface RawPartnerRow {
  id: string
  name: string
  remarks: string | null
  is_active: number | null
  created_at: string | null
  updated_at: string | null
}

function mapPartner(r: RawPartnerRow): ReferralPartner {
  const now = new Date().toISOString()
  return {
    id: r.id,
    name: r.name,
    remarks: r.remarks,
    isActive: r.is_active === null ? true : Boolean(r.is_active),
    createdAt: r.created_at ?? now,
    updatedAt: r.updated_at ?? r.created_at ?? now,
  }
}

export function useReferralPartnersList() {
  return useLocalQuery<ReferralPartner[]>(
    ['referral_partners'],
    async () => {
      const db = getPowerSyncInstance()
      const rows = await db.getAll<RawPartnerRow>(
        'SELECT * FROM referral_partners ORDER BY is_active DESC, name ASC',
      )
      return rows.map(mapPartner)
    },
  )
}

export function useCreateReferralPartner() {
  return useMutation({
    mutationFn: async (input: CreateReferralPartnerInput): Promise<ReferralPartner> => {
      const db = getPowerSyncInstance()
      const id = uuidv7()
      const now = new Date().toISOString()
      const name = input.name.trim()
      const remarks = input.remarks?.trim() || null
      const isActive = input.isActive ?? true
      await db.execute(
        `INSERT INTO referral_partners (id, version, updated_at, name, remarks, is_active, created_at)
         VALUES (?, 1, ?, ?, ?, ?, ?)`,
        [id, now, name, remarks, isActive ? 1 : 0, now],
      )
      return { id, name, remarks, isActive, createdAt: now, updatedAt: now }
    },
  })
}

export function useUpdateReferralPartner() {
  return useMutation({
    mutationFn: async (input: UpdateReferralPartnerInput): Promise<ReferralPartner> => {
      const db = getPowerSyncInstance()
      const now = new Date().toISOString()
      const name = input.name.trim()
      const remarks = input.remarks?.trim() || null
      await db.execute(
        `UPDATE referral_partners
         SET name = ?, remarks = ?, is_active = ?, updated_at = ?
         WHERE id = ?`,
        [name, remarks, input.isActive ? 1 : 0, now, input.id],
      )
      return {
        id: input.id,
        name,
        remarks,
        isActive: input.isActive,
        createdAt: now,
        updatedAt: now,
      }
    },
  })
}

export function useDeleteReferralPartner() {
  return useMutation({
    mutationFn: async (id: string) => {
      const db = getPowerSyncInstance()
      const inUse = await db.getOptional<{ id: string }>(
        'SELECT id FROM referrals WHERE referred_to_id = ? LIMIT 1',
        [id],
      )
      if (inUse) {
        throw new Error('A referral still references this partner. Deactivate it instead.')
      }
      await db.execute('DELETE FROM referral_partners WHERE id = ?', [id])
    },
  })
}

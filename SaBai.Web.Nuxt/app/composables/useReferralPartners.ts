import { useQuery, useQueryClient } from '@tanstack/vue-query'
import {
  createReferralPartner,
  deleteReferralPartner,
  fetchReferralPartners,
  updateReferralPartner,
  type ReferralPartner,
} from '~/offline/mutations/referrals'
import { uuidv7 } from '~/utils/uuid'

export type { ReferralPartner } from '~/offline/mutations/referrals'

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

/** Partners, active first and then by name. Served from the cache (also offline) and refreshed from the API. */
export function useReferralPartnersList() {
  return useQuery({
    queryKey: queryKeys.referralPartners(),
    queryFn: ({ signal }) => fetchReferralPartners(signal),
  })
}

export function useCreateReferralPartner() {
  return useOfflineWrite(createReferralPartner, (input: CreateReferralPartnerInput) => ({
    id: uuidv7(),
    name: input.name.trim(),
    remarks: input.remarks?.trim() || null,
    isActive: input.isActive ?? true,
  }))
}

export function useUpdateReferralPartner() {
  return useOfflineWrite(updateReferralPartner, (input: UpdateReferralPartnerInput) => ({
    id: input.id,
    name: input.name.trim(),
    remarks: input.remarks?.trim() || null,
    isActive: input.isActive,
  }))
}

export function useDeleteReferralPartner() {
  const queryClient = useQueryClient()
  return useOfflineWrite(deleteReferralPartner, (id: string) => ({
    id,
    name: queryClient.getQueryData<ReferralPartner[]>(queryKeys.referralPartners())?.find(p => p.id === id)?.name ?? 'partner',
  }))
}

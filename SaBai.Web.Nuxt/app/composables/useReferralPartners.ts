import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

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

export function useReferralPartnersList() {
  return useQuery({
    queryKey: queryKeys.referralPartners(),
    queryFn: ({ signal }) =>
      $fetch<ReferralPartner[]>('/api/referral-partners', {
        credentials: 'same-origin',
        signal,
      }),
  })
}

export function useCreateReferralPartner() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: CreateReferralPartnerInput) =>
      $fetch<ReferralPartner>('/api/referral-partners', {
        method: 'POST',
        body: {
          name: input.name.trim(),
          remarks: input.remarks?.trim() || null,
          isActive: input.isActive ?? true,
        },
        credentials: 'same-origin',
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.referralPartners() })
    },
  })
}

export function useUpdateReferralPartner() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: UpdateReferralPartnerInput) =>
      $fetch<ReferralPartner>(`/api/referral-partners/${input.id}`, {
        method: 'PUT',
        body: {
          id: input.id,
          name: input.name.trim(),
          remarks: input.remarks?.trim() || null,
          isActive: input.isActive,
        },
        credentials: 'same-origin',
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.referralPartners() })
    },
  })
}

export function useDeleteReferralPartner() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      $fetch(`/api/referral-partners/${id}`, {
        method: 'DELETE',
        credentials: 'same-origin',
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.referralPartners() })
    },
  })
}

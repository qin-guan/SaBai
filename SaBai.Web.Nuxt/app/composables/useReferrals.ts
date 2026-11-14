import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import type { MaybeRefOrGetter } from 'vue'
import {
  saBaiAPIEndpointsReferralsCreatePostEndpoint,
  saBaiAPIEndpointsReferralsGetEndpoint,
  saBaiAPIEndpointsReferralsListByConsultGetEndpoint,
  saBaiAPIEndpointsReferralsListGetEndpoint,
  saBaiAPIEndpointsReferralsUpdateStatePatchEndpoint,
} from '@sabai/sdk/clients'
import type {
  SaBaiAPIDomainReferralStateKey,
  SaBaiAPIEndpointsReferralsGetResponse,
  SaBaiAPIEndpointsReferralsListGetResponse,
} from '@sabai/sdk/types'

export interface CreateReferralInput {
  consultId: number
  referredFor: string
  referralNotes?: string
}

export interface UpdateReferralStateInput {
  id: number
  referralState: SaBaiAPIDomainReferralStateKey
  referralOutcome?: string
}

export interface UpdateReferralOutcomeInput {
  id: number
  referralOutcome: string
}

export interface UpdateReferralBoothInput {
  id: number
  referredToId: string | null
  memberInChargeId: string | null
  guardiansContacted: GuardiansContacted | null
  classOfService: ReferralServiceClass | null
  descriptionOfService: string | null
  patientSummaryFromScreening: string | null
  remarksAtBooth: string | null
}

export interface UpdateReferralPartyInput {
  id: number
  memberOnRunId: string | null
  presentAtReferralParty: boolean
  referralPartyResults: string | null
  followUp: boolean
  doctorsUpdated: boolean
}

export interface UpdateReferralMoneyInput {
  id: number
  amountPaidUsd: number | null
  amountPaidRiels: number | null
  memberWhoPaidId: string | null
}

/**
 * List of all clinic referrals, newest first.
 */
export function useReferralsList() {
  return useQuery({
    queryKey: queryKeys.referrals(),
    queryFn: ({ signal }) =>
      saBaiAPIEndpointsReferralsListGetEndpoint({
        ...apiConfig,
        signal,
      }).unwrap(),
  })
}

/**
 * Single referral with consult, patient overview, booth, party, and money context.
 */
export function useReferral(id: MaybeRefOrGetter<number | null | undefined>) {
  return useQuery({
    queryKey: computed(() => queryKeys.referral(toValue(id) ?? 0)),
    enabled: computed(() => {
      const v = toValue(id)
      return typeof v === 'number' && Number.isFinite(v) && v > 0
    }),
    queryFn: ({ signal }) =>
      saBaiAPIEndpointsReferralsGetEndpoint({
        ...apiConfig,
        path: { id: toValue(id)! },
        signal,
      }).unwrap() as Promise<ReferralDetail>,
  })
}

/**
 * Referrals opened during a specific consult.
 */
export function useReferralsByConsult(consultId: MaybeRefOrGetter<number>) {
  return useQuery({
    queryKey: computed(() => queryKeys.referralsByConsult(toValue(consultId))),
    enabled: computed(() => Number.isFinite(toValue(consultId)) && toValue(consultId) > 0),
    queryFn: ({ signal }) =>
      saBaiAPIEndpointsReferralsListByConsultGetEndpoint({
        ...apiConfig,
        path: { consultId: toValue(consultId) },
        signal,
      }).unwrap(),
  })
}

/**
 * Opens a new referral against a saved consult.
 */
export function useCreateReferral() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: CreateReferralInput) =>
      saBaiAPIEndpointsReferralsCreatePostEndpoint({
        ...apiConfig,
        body: {
          consultId: input.consultId,
          referredFor: input.referredFor.trim(),
          referralNotes: input.referralNotes?.trim() || undefined,
        },
        throwOnError: true,
      }).unwrap(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.referrals() })
    },
  })
}

/**
 * Updates a referral's status in the care lifecycle.
 */
export function useUpdateReferralState() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: UpdateReferralStateInput) =>
      saBaiAPIEndpointsReferralsUpdateStatePatchEndpoint({
        ...apiConfig,
        path: { id: input.id },
        body: {
          referralState: input.referralState,
          referralOutcome: input.referralOutcome,
        },
        throwOnError: true,
      }).unwrap(),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.referrals() })
      queryClient.invalidateQueries({ queryKey: queryKeys.referral(variables.id) })
    },
  })
}

/**
 * Updates only the referral's outcome notes, avoiding race conditions
 * where a concurrent status change could be overwritten.
 */
export function useUpdateReferralOutcome() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: UpdateReferralOutcomeInput) => {
      return await $fetch<ReferralDetail>(
        `/api/referrals/${input.id}/outcome`,
        {
          method: 'PATCH',
          body: {
            id: input.id,
            referralOutcome: input.referralOutcome,
          },
          credentials: 'same-origin',
        },
      )
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.referrals() })
      queryClient.invalidateQueries({ queryKey: queryKeys.referral(variables.id) })
    },
  })
}

/**
 * Updates only the referral booth fields so saving booth details cannot overwrite
 * concurrent edits to party or money sections.
 */
export function useUpdateReferralBooth() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: UpdateReferralBoothInput) => {
      return await $fetch<ReferralDetail>(
        `/api/referrals/${input.id}/booth`,
        {
          method: 'PATCH',
          body: input,
          credentials: 'same-origin',
        },
      )
    },
    onSuccess: (updated, variables) => {
      queryClient.setQueryData(queryKeys.referral(variables.id), updated)
      queryClient.invalidateQueries({ queryKey: queryKeys.referrals() })
    },
  })
}

/**
 * Updates only the referral party fields.
 */
export function useUpdateReferralParty() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: UpdateReferralPartyInput) => {
      return await $fetch<ReferralDetail>(
        `/api/referrals/${input.id}/party`,
        {
          method: 'PATCH',
          body: input,
          credentials: 'same-origin',
        },
      )
    },
    onSuccess: (updated, variables) => {
      queryClient.setQueryData(queryKeys.referral(variables.id), updated)
      queryClient.invalidateQueries({ queryKey: queryKeys.referrals() })
    },
  })
}

/**
 * Updates only the referral money fields.
 */
export function useUpdateReferralMoney() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: UpdateReferralMoneyInput) => {
      return await $fetch<ReferralDetail>(
        `/api/referrals/${input.id}/money`,
        {
          method: 'PATCH',
          body: input,
          credentials: 'same-origin',
        },
      )
    },
    onSuccess: (updated, variables) => {
      queryClient.setQueryData(queryKeys.referral(variables.id), updated)
      queryClient.invalidateQueries({ queryKey: queryKeys.referrals() })
    },
  })
}

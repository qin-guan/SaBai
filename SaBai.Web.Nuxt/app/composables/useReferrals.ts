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
 * Single referral with consult and patient context.
 */
export function useReferral(id: MaybeRefOrGetter<number>) {
  return useQuery({
    queryKey: computed(() => queryKeys.referral(toValue(id))),
    enabled: computed(() => Number.isFinite(toValue(id)) && toValue(id) > 0),
    queryFn: ({ signal }) =>
      saBaiAPIEndpointsReferralsGetEndpoint({
        ...apiConfig,
        path: { id: toValue(id) },
        signal,
      }).unwrap(),
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
          id: input.id,
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
      return await $fetch<SaBaiAPIEndpointsReferralsGetResponse>(
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

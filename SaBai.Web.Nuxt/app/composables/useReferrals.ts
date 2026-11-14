import { useQuery } from '@tanstack/vue-query'
import type { MaybeRefOrGetter } from 'vue'
import {
  createReferral,
  fetchReferral,
  fetchReferrals,
  fetchReferralsByConsult,
  reimburseReferralBills,
  repayPnBills,
  updateReferralBooth,
  updateReferralMoney,
  updateReferralOutcome,
  updateReferralParty,
  updateReferralReimbursement,
  updateReferralState,
  type CreateReferralContext,
  type ReferralDetail,
  type ReimburseReferralBillsVars,
  type RepayPnBillsVars,
  type UpdateReferralBoothVars,
  type UpdateReferralMoneyVars,
  type UpdateReferralOutcomeVars,
  type UpdateReferralPartyVars,
  type UpdateReferralReimbursementVars,
  type UpdateReferralStateVars,
} from '~/offline/mutations/referrals'
import type { OfflineMutationDef } from '~/offline/defineOfflineMutation'
import type { OfflineMutationOutcome } from '~/offline/mutate'
import { uuidv7 } from '~/utils/uuid'

export type { CreateReferralContext } from '~/offline/mutations/referrals'

export interface CreateReferralInput {
  /** The client id of the referral; generated when omitted. */
  id?: string
  consultId: string
  referredFor: string
  referralNotes?: string | null
  /** Lets the referral appear in the referral list and open while it is still waiting to be sent. */
  context?: CreateReferralContext
}

export type UpdateReferralStateInput = UpdateReferralStateVars
export type UpdateReferralOutcomeInput = UpdateReferralOutcomeVars
export type UpdateReferralBoothInput = UpdateReferralBoothVars
export type UpdateReferralPartyInput = UpdateReferralPartyVars
export type UpdateReferralMoneyInput = UpdateReferralMoneyVars

interface MutateCallbacks<TResult> {
  onSuccess?: (outcome: OfflineMutationOutcome<TResult>) => void
  onError?: (error: unknown) => void
}

/**
 * An offline write run like a `useMutation`: `mutateAsync` resolves with the
 * outcome (sent now, or saved on this device), `mutate` reports it through
 * callbacks. `toVars` turns the caller's input into the stored request.
 */
export function useOfflineWrite<TInput, TVars, TResult>(def: OfflineMutationDef<TVars, TResult>, toVars: (input: TInput) => TVars) {
  const { mutateAsync: run, isPending } = useOfflineMutation(def)
  const mutateAsync = (input: TInput) => run(toVars(input))
  function mutate(input: TInput, callbacks: MutateCallbacks<TResult> = {}) {
    mutateAsync(input).then(callbacks.onSuccess, callbacks.onError)
  }
  return { mutate, mutateAsync, isPending }
}

/**
 * List of all clinic referrals, newest first.
 */
export function useReferralsList() {
  return useQuery({
    queryKey: queryKeys.referrals(),
    queryFn: ({ signal }) => fetchReferrals(signal),
  })
}

/**
 * Single referral with consult, patient overview, booth, party, and money context.
 */
export function useReferral(id: MaybeRefOrGetter<string | null | undefined>) {
  return useQuery({
    queryKey: computed(() => queryKeys.referral(toValue(id) ?? '')),
    enabled: computed(() => !!toValue(id)),
    queryFn: ({ signal }): Promise<ReferralDetail> => fetchReferral(toValue(id)!, signal),
  })
}

/**
 * Referrals opened during a specific consult.
 */
export function useReferralsByConsult(consultId: MaybeRefOrGetter<string | null | undefined>) {
  return useQuery({
    queryKey: computed(() => queryKeys.referralsByConsult(toValue(consultId) ?? '')),
    enabled: computed(() => !!toValue(consultId)),
    queryFn: ({ signal }) => fetchReferralsByConsult(toValue(consultId)!, signal),
  })
}

/**
 * Opens a new referral against a saved consult. Works offline: the referral gets
 * a client id and is sent with the rest of the queue.
 */
export function useCreateReferral() {
  return useOfflineWrite(createReferral, (input: CreateReferralInput) => ({
    id: input.id ?? uuidv7(),
    consultId: input.consultId,
    referredFor: input.referredFor.trim(),
    referralNotes: input.referralNotes?.trim() || null,
    context: input.context,
  }))
}

/**
 * Updates a referral's status in the care lifecycle.
 */
export function useUpdateReferralState() {
  return useOfflineWrite(updateReferralState, (input: UpdateReferralStateInput) => input)
}

/**
 * Updates only the referral's outcome notes, avoiding race conditions
 * where a concurrent status change could be overwritten.
 */
export function useUpdateReferralOutcome() {
  return useOfflineWrite(updateReferralOutcome, (input: UpdateReferralOutcomeInput) => input)
}

/**
 * Updates only the referral booth fields so saving booth details cannot overwrite
 * concurrent edits to party or money sections.
 */
export function useUpdateReferralBooth() {
  return useOfflineWrite(updateReferralBooth, (input: UpdateReferralBoothInput) => input)
}

/**
 * Updates only the referral party fields.
 */
export function useUpdateReferralParty() {
  return useOfflineWrite(updateReferralParty, (input: UpdateReferralPartyInput) => input)
}

/**
 * Updates only the referral money fields.
 */
export function useUpdateReferralMoney() {
  return useOfflineWrite(updateReferralMoney, (input: UpdateReferralMoneyInput) => input)
}

export type UpdateReferralReimbursementInput = UpdateReferralReimbursementVars
export type ReimburseReferralBillsInput = ReimburseReferralBillsVars
export type RepayPnBillsInput = RepayPnBillsVars

/**
 * Marks a single referral bill as reimbursed or pending reimbursement.
 */
export function useUpdateReferralReimbursement() {
  return useOfflineWrite(updateReferralReimbursement, (input: UpdateReferralReimbursementInput) => input)
}

/**
 * Bulk marks multiple referral bills as reimbursed or pending reimbursement.
 */
export function useReimburseReferralBills() {
  return useOfflineWrite(reimburseReferralBills, (input: ReimburseReferralBillsInput) => input)
}

/**
 * Applies a full or partial lump-sum repayment to a PN member's pending bills (FIFO).
 */
export function useRepayPnBills() {
  return useOfflineWrite(repayPnBills, (input: RepayPnBillsInput) => input)
}

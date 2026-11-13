import type { Option } from './dental'
import { cleanKhmerText, matchesSearchText } from './khmer'
import { formatPatientCode } from './patient'
import type {
  SaBaiAPIDomainReferralStateKey,
  SaBaiAPIEndpointsReferralsListGetResponse,
} from '@sabai/sdk/types'

export type ReferralListItem = SaBaiAPIEndpointsReferralsListGetResponse

/**
 * Natural lifecycle order of referral states.
 */
export const REFERRAL_STATES: readonly SaBaiAPIDomainReferralStateKey[] = [
  'New',
  'Seen',
  'Outgoing',
  'Completed',
  'CompletedSuccess',
  'CompletedFailure',
  'None',
] as const

export const REFERRAL_STATE_LABELS: Record<SaBaiAPIDomainReferralStateKey, string> = {
  New: 'New',
  Seen: 'Seen',
  Outgoing: 'Outgoing',
  Completed: 'Completed',
  CompletedSuccess: 'Completed (Success)',
  CompletedFailure: 'Completed (Failure)',
  None: 'None',
}

export const REFERRAL_STATE_OPTIONS: Option<SaBaiAPIDomainReferralStateKey>[] = REFERRAL_STATES.map(state => ({
  value: state,
  label: REFERRAL_STATE_LABELS[state],
}))

export const REFERRAL_CATEGORIES = [
  'Not Referred',
  'Diagnostic',
  'Acute',
  'Chronic',
  'AdvancedVision',
  'Others',
] as const

export type ReferralCategory = (typeof REFERRAL_CATEGORIES)[number]

export const REFERRAL_CATEGORY_OPTIONS: Option<ReferralCategory>[] = REFERRAL_CATEGORIES.map(category => ({
  value: category,
  label: category === 'AdvancedVision' ? 'AdvancedVision [Within clinic]' : category,
}))

/**
 * Badge style classes for each referral state.
 */
export function referralStateBadgeClass(state: SaBaiAPIDomainReferralStateKey): string {
  switch (state) {
    case 'New':
      return 'bg-blue-100 text-blue-800 border-blue-200 dark:bg-blue-950 dark:text-blue-300 dark:border-blue-800'
    case 'Seen':
      return 'bg-amber-100 text-amber-800 border-amber-200 dark:bg-amber-950 dark:text-amber-300 dark:border-amber-800'
    case 'Outgoing':
      return 'bg-purple-100 text-purple-800 border-purple-200 dark:bg-purple-950 dark:text-purple-300 dark:border-purple-800'
    case 'Completed':
    case 'CompletedSuccess':
      return 'bg-emerald-100 text-emerald-800 border-emerald-200 dark:bg-emerald-950 dark:text-emerald-300 dark:border-emerald-800'
    case 'CompletedFailure':
      return 'bg-rose-100 text-rose-800 border-rose-200 dark:bg-rose-950 dark:text-rose-300 dark:border-rose-800'
    case 'None':
    default:
      return 'bg-slate-100 text-slate-800 border-slate-200 dark:bg-slate-900 dark:text-slate-300 dark:border-slate-800'
  }
}

export interface ReferralFilterOptions {
  search?: string
  villageCodeId?: number | null
  referralState?: SaBaiAPIDomainReferralStateKey | null
}

/**
 * Filters a referral against search query, village, and state.
 * Supports Khmer script and numeral normalization.
 */
export function matchesReferral(item: ReferralListItem, filters: ReferralFilterOptions): boolean {
  if (filters.villageCodeId && item.villageCodeId !== filters.villageCodeId)
    return false

  if (filters.referralState && item.referralState !== filters.referralState)
    return false

  const needle = cleanKhmerText(filters.search ?? '')
  if (!needle)
    return true

  const patientCode = formatPatientCode(item.villageCode, item.patientId)
  const targets = [
    item.patientName,
    patientCode,
    String(item.patientId),
    item.villageCode,
    item.referredFor,
    item.referralNotes ?? '',
    item.referralOutcome ?? '',
    REFERRAL_STATE_LABELS[item.referralState] ?? item.referralState,
  ]

  return targets.some(target => matchesSearchText(target, needle))
}

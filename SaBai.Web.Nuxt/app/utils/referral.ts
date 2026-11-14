import type { Option } from './options'
import { cleanKhmerText, matchesSearchText } from './khmer'
import { formatPatientCode } from './patient'
import type { SaBaiAPIDomainReferralStateKey } from '@sabai/sdk/types'
import type { LocalReferralItem } from '~/composables/useReferrals'

export type ReferralListItem = LocalReferralItem

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
 * Badge style classes for each referral state adhering to the clinical EHR design system.
 */
export function referralStateBadgeClass(state: SaBaiAPIDomainReferralStateKey): string {
  switch (state) {
    case 'New':
      return 'border-warning-border bg-warning-subtle text-warning'
    case 'Seen':
      return 'border-band-border bg-accent text-accent-foreground'
    case 'Outgoing':
      return 'border-chrome/40 bg-band text-chrome'
    case 'Completed':
    case 'CompletedSuccess':
      return 'border-success/40 bg-success-subtle text-success'
    case 'CompletedFailure':
      return 'border-destructive/40 bg-destructive-subtle text-destructive'
    case 'None':
    default:
      return 'border-border bg-muted text-muted-foreground'
  }
}

export interface ReferralFilterOptions {
  search?: string
  villageCodeId?: string | null
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

  const targets = [
    item.patientName,
    item.patientDisplayId,
    String(item.patientId),
    item.villageCode ?? '',
    item.referredFor,
    item.referralNotes ?? '',
    item.referralOutcome ?? '',
    REFERRAL_STATE_LABELS[item.referralState] ?? item.referralState,
  ]

  return targets.some(target => matchesSearchText(target, needle))
}

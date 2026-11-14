import type { Option } from './options'
import { cleanKhmerText, matchesSearchText } from './khmer'
import { calculateAge, formatPatientCode, matchesTags, type TagMatchMode } from './patient'
import type {
  SaBaiAPIDomainReferralStateKey,
  SaBaiAPIEndpointsReferralsGetResponse,
  SaBaiAPIEndpointsReferralsListGetResponse,
} from '@sabai/sdk/types'

export type ReferralListItem = SaBaiAPIEndpointsReferralsListGetResponse & {
  tags?: string[]
}

export const GUARDIANS_CONTACTED = ['Yes', 'No', 'NotApplicable'] as const
export type GuardiansContacted = (typeof GUARDIANS_CONTACTED)[number]

export const GUARDIANS_CONTACTED_LABELS: Record<GuardiansContacted, string> = {
  Yes: 'Yes',
  No: 'No',
  NotApplicable: 'Not Applicable',
}

export const REFERRAL_SERVICE_CLASSES = [
  'ChronicManagement',
  'ImagingDiagnostics',
  'DefinitiveTreatment',
  'SymptomaticTreatment',
] as const
export type ReferralServiceClass = (typeof REFERRAL_SERVICE_CLASSES)[number]

export const REFERRAL_SERVICE_CLASS_LABELS: Record<ReferralServiceClass, string> = {
  ChronicManagement: 'Chronic Management',
  ImagingDiagnostics: 'Imaging/Diagnostics',
  DefinitiveTreatment: 'Definitive Treatment',
  SymptomaticTreatment: 'Symptomatic Treatment',
}

export const REFERRAL_SERVICE_CLASS_OPTIONS: Option<ReferralServiceClass>[] = REFERRAL_SERVICE_CLASSES.map(c => ({
  value: c,
  label: REFERRAL_SERVICE_CLASS_LABELS[c],
}))

export const ADULT_AGE = 18
export const USD_AMOUNT_PATTERN = /^\d{1,8}(\.\d{1,2})?$/
export const MAX_RIELS = 2_147_483_647

export type ReferralDetail = SaBaiAPIEndpointsReferralsGetResponse & {
  updatedAt?: string
  patientDateOfBirth?: string
  patientContactNo?: string | null
  patientHasPoorCard?: boolean
  patientHasBS2Card?: boolean
  patientHasSabaiCard?: boolean
  referredToId?: string | null
  memberInChargeId?: string | null
  guardiansContacted?: GuardiansContacted | null
  classOfService?: ReferralServiceClass | null
  descriptionOfService?: string | null
  patientSummaryFromScreening?: string | null
  remarksAtBooth?: string | null
  memberOnRunId?: string | null
  presentAtReferralParty?: boolean
  referralPartyResults?: string | null
  followUp?: boolean
  doctorsUpdated?: boolean
  amountPaidUsd?: number | string | null
  amountPaidRiels?: number | null
  memberWhoPaidId?: string | null
}

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
  villageCodeId?: number | null
  referralState?: SaBaiAPIDomainReferralStateKey | null
  referralStates?: readonly SaBaiAPIDomainReferralStateKey[] | null
  tags?: readonly string[] | null
  tagMatchMode?: TagMatchMode
}

/**
 * Filters a referral against search query, village, state(s), and patient tags.
 * Supports Khmer script and numeral normalization.
 */
export function matchesReferral(item: ReferralListItem, filters: ReferralFilterOptions): boolean {
  if (filters.villageCodeId && item.villageCodeId !== filters.villageCodeId)
    return false

  if (filters.referralState && item.referralState !== filters.referralState)
    return false

  if (filters.referralStates && filters.referralStates.length > 0 && !filters.referralStates.includes(item.referralState))
    return false

  if (!matchesTags(item.tags, filters.tags, filters.tagMatchMode ?? 'any'))
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
    ...(item.tags ?? []),
  ]

  return targets.some(target => matchesSearchText(target, needle))
}

/** Status groups for the list's summary strip. Completed covers all three completed states. */
export const REFERRAL_STATUS_GROUPS = [
  { key: 'New', label: 'New', states: ['New'] },
  { key: 'Seen', label: 'Seen', states: ['Seen'] },
  { key: 'Outgoing', label: 'Outgoing', states: ['Outgoing'] },
  { key: 'Completed', label: 'Completed', states: ['Completed', 'CompletedSuccess', 'CompletedFailure'] },
  { key: 'None', label: 'None', states: ['None'] },
] as const satisfies readonly { key: string, label: string, states: readonly SaBaiAPIDomainReferralStateKey[] }[]
export type ReferralStatusGroup = (typeof REFERRAL_STATUS_GROUPS)[number]['key']

/** Whether a referral state belongs to a status group (no group = every referral). */
export function inReferralStatusGroup(state: SaBaiAPIDomainReferralStateKey, group: ReferralStatusGroup | null): boolean {
  if (!group)
    return true
  return (REFERRAL_STATUS_GROUPS.find(g => g.key === group)?.states as readonly string[] | undefined)?.includes(state) ?? false
}

/*
 * The referral workflow form: four steps (booth → party → payment → outcome)
 * edited on one page and saved together. Each step still saves through its
 * own endpoint, so only the steps that changed are sent.
 */

export const REFERRAL_STEPS = ['booth', 'party', 'money', 'outcome'] as const
export type ReferralStep = (typeof REFERRAL_STEPS)[number]

export const REFERRAL_STEP_LABELS: Record<ReferralStep, string> = {
  booth: 'Referral booth',
  party: 'Referral party',
  money: 'Payment',
  outcome: 'Outcome',
}

/** Everything the referral page saves together: the status plus the four steps. */
export const REFERRAL_FORM_PARTS = ['status', ...REFERRAL_STEPS] as const
export type ReferralFormPart = (typeof REFERRAL_FORM_PARTS)[number]

export const REFERRAL_PART_LABELS: Record<ReferralFormPart, string> = {
  status: 'Status',
  ...REFERRAL_STEP_LABELS,
}

export interface ReferralFormValues {
  status: {
    referralState: SaBaiAPIDomainReferralStateKey
  }
  booth: {
    referredToId: string
    classOfService: string
    memberInChargeId: string
    guardiansContacted: boolean
    descriptionOfService: string
    patientSummaryFromScreening: string
    remarksAtBooth: string
  }
  party: {
    memberOnRunId: string
    presentAtReferralParty: boolean
    referralPartyResults: string
    followUp: boolean
    doctorsUpdated: boolean
  }
  money: {
    amountPaidUsd: string
    amountPaidRiels: string
    memberWhoPaidId: string
  }
  outcome: {
    referralOutcome: string
  }
}

type ReferralFields = Partial<Omit<ReferralDetail, 'amountPaidRiels'>> & {
  amountPaidRiels?: number | string | null
}

/** Form values for a saved referral (empty strings for anything not recorded). */
export function referralFormValues(r: ReferralFields): ReferralFormValues {
  return {
    status: {
      referralState: r.referralState ?? 'New',
    },
    booth: {
      referredToId: r.referredToId ?? '',
      classOfService: r.classOfService ?? '',
      memberInChargeId: r.memberInChargeId ?? '',
      guardiansContacted: r.guardiansContacted === 'Yes',
      descriptionOfService: r.descriptionOfService ?? '',
      patientSummaryFromScreening: r.patientSummaryFromScreening ?? '',
      remarksAtBooth: r.remarksAtBooth ?? '',
    },
    party: {
      memberOnRunId: r.memberOnRunId ?? '',
      presentAtReferralParty: !!r.presentAtReferralParty,
      referralPartyResults: r.referralPartyResults ?? '',
      followUp: !!r.followUp,
      doctorsUpdated: !!r.doctorsUpdated,
    },
    money: {
      amountPaidUsd: r.amountPaidUsd != null ? String(r.amountPaidUsd) : '',
      amountPaidRiels: r.amountPaidRiels != null ? String(r.amountPaidRiels) : '',
      memberWhoPaidId: r.memberWhoPaidId ?? '',
    },
    outcome: {
      referralOutcome: r.referralOutcome ?? '',
    },
  }
}

/** Parts (status and steps) whose values differ between `a` and `b`, in page order. */
export function changedReferralParts(a: ReferralFormValues, b: ReferralFormValues): ReferralFormPart[] {
  return REFERRAL_FORM_PARTS.filter(part => JSON.stringify(a[part]) !== JSON.stringify(b[part]))
}

/** Adults are not asked about guardians. */
export function isAdultAt(dateOfBirth: string | null | undefined, at: string | Date): boolean {
  if (!dateOfBirth)
    return false
  return calculateAge(dateOfBirth, at) >= ADULT_AGE
}

export interface ReferralStepPayloads {
  status: {
    referralState: SaBaiAPIDomainReferralStateKey
  }
  booth: {
    referredToId: string | null
    memberInChargeId: string | null
    guardiansContacted: GuardiansContacted
    classOfService: ReferralServiceClass | null
    descriptionOfService: string | null
    patientSummaryFromScreening: string | null
    remarksAtBooth: string | null
  }
  party: {
    memberOnRunId: string | null
    presentAtReferralParty: boolean
    referralPartyResults: string | null
    followUp: boolean
    doctorsUpdated: boolean
  }
  money: {
    amountPaidUsd: number | null
    amountPaidRiels: number | null
    memberWhoPaidId: string | null
  }
  outcome: {
    referralOutcome: string
  }
}

/** The request body for one part (without the referral id). Text is trimmed and blanks sent as null. */
export function referralStepPayload<S extends ReferralFormPart>(step: S, values: ReferralFormValues, opts: { isAdult: boolean }): ReferralStepPayloads[S] {
  const text = (v: string) => v.trim() || null
  const payloads = {
    status: (): ReferralStepPayloads['status'] => ({ referralState: values.status.referralState }),
    booth: (): ReferralStepPayloads['booth'] => {
      const b = values.booth
      return {
        referredToId: b.referredToId || null,
        memberInChargeId: b.memberInChargeId || null,
        guardiansContacted: opts.isAdult ? 'NotApplicable' : b.guardiansContacted ? 'Yes' : 'No',
        classOfService: (b.classOfService || null) as ReferralServiceClass | null,
        descriptionOfService: text(b.descriptionOfService),
        patientSummaryFromScreening: text(b.patientSummaryFromScreening),
        remarksAtBooth: text(b.remarksAtBooth),
      }
    },
    party: (): ReferralStepPayloads['party'] => {
      const p = values.party
      return {
        memberOnRunId: p.memberOnRunId || null,
        presentAtReferralParty: p.presentAtReferralParty,
        referralPartyResults: text(p.referralPartyResults),
        followUp: p.followUp,
        doctorsUpdated: p.doctorsUpdated,
      }
    },
    money: (): ReferralStepPayloads['money'] => {
      const usd = values.money.amountPaidUsd.trim()
      const riels = values.money.amountPaidRiels.trim()
      return {
        amountPaidUsd: usd ? Number(usd) : null,
        amountPaidRiels: riels ? Number(riels) : null,
        memberWhoPaidId: values.money.memberWhoPaidId || null,
      }
    },
    outcome: (): ReferralStepPayloads['outcome'] => ({ referralOutcome: values.outcome.referralOutcome.trim() }),
  }
  return payloads[step]() as ReferralStepPayloads[S]
}

export interface ReferralMoneyErrors {
  amountPaidUsd?: string
  amountPaidRiels?: string
}

/** Validation errors for the payment step (empty when valid; blank amounts are allowed). */
export function validateReferralMoney(money: ReferralFormValues['money']): ReferralMoneyErrors {
  const errors: ReferralMoneyErrors = {}
  const usd = money.amountPaidUsd.trim()
  const riels = money.amountPaidRiels.trim()
  if (usd && !USD_AMOUNT_PATTERN.test(usd))
    errors.amountPaidUsd = 'Enter dollars with up to 2 decimals, e.g. 12.50.'
  if (riels && (!/^\d+$/.test(riels) || Number(riels) > MAX_RIELS))
    errors.amountPaidRiels = 'Enter a whole number of riels, e.g. 40000.'
  return errors
}

/** Whether anything has been recorded for a step yet. */
export function isReferralStepStarted(step: ReferralStep, values: ReferralFormValues): boolean {
  return Object.values(values[step]).some(x => (typeof x === 'string' ? x.trim() !== '' : x))
}

/** One-line summary of what a step holds, for the progress list. */
export function referralStepSummary(
  step: ReferralStep,
  values: ReferralFormValues,
  names: { partner: (id: string) => string | undefined, member: (id: string) => string | undefined },
): string {
  if (!isReferralStepStarted(step, values))
    return 'Not recorded yet'
  switch (step) {
    case 'booth': {
      const b = values.booth
      const parts = [
        b.referredToId && (names.partner(b.referredToId) ?? 'Clinic chosen'),
        b.classOfService && REFERRAL_SERVICE_CLASS_LABELS[b.classOfService as ReferralServiceClass],
      ].filter(Boolean)
      return parts.length ? parts.join(', ') : 'Notes recorded'
    }
    case 'party': {
      const p = values.party
      const parts = [
        p.presentAtReferralParty ? 'Attended' : null,
        p.memberOnRunId ? `Run by ${names.member(p.memberOnRunId) ?? 'PN member'}` : null,
        p.followUp ? 'Follow up' : null,
        p.doctorsUpdated ? 'Doctors updated' : null,
      ].filter(Boolean)
      return parts.length ? parts.join(', ') : 'Results recorded'
    }
    case 'money': {
      const m = values.money
      const parts = [
        m.amountPaidUsd.trim() && `US$${m.amountPaidUsd.trim()}`,
        m.amountPaidRiels.trim() && `${Number(m.amountPaidRiels).toLocaleString('en-US')} riels`,
      ].filter(Boolean)
      return parts.length ? parts.join(' + ') : `Paid by ${names.member(m.memberWhoPaidId) ?? 'PN member'}`
    }
    case 'outcome': {
      const line = values.outcome.referralOutcome.trim().split('\n')[0]!
      return line.length > 80 ? `${line.slice(0, 79)}…` : line
    }
  }
}

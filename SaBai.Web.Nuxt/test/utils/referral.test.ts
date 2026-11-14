import { describe, expect, it } from 'vitest'
import {
  changedReferralParts,
  inReferralStatusGroup,
  isAdultAt,
  isReferralStepStarted,
  matchesReferral,
  referralFormValues,
  referralStepPayload,
  referralStepSummary,
  validateReferralMoney,
  REFERRAL_CATEGORIES,
  REFERRAL_CATEGORY_OPTIONS,
  REFERRAL_STATE_LABELS,
  REFERRAL_STATE_OPTIONS,
  REFERRAL_STATES,
  referralStateBadgeClass,
  type ReferralListItem,
} from '~/utils/referral'

const sampleReferral: ReferralListItem = {
  id: '01930000-0000-7000-8000-000000000001',
  consultId: '01930000-0000-7000-8000-000000000010',
  visitId: '01930000-0000-7000-8000-000000000020',
  patientId: '01930000-0000-7000-8000-000000000042',
  patientName: 'Sokha Chan',
  patientNumber: 42,
  patientDisplayId: 'SB0042',
  patientDateOfBirth: '1990-01-01T00:00:00Z',
  patientContactNo: '012345678',
  patientHasPoorCard: false,
  patientHasBS2Card: false,
  patientHasSabaiCard: true,
  hasPhoto: true,
  villageCodeId: '01930000-0000-7000-8000-000000000002',
  villageCode: 'SB',
  villageColorHex: '#2563eb',
  referredFor: 'Diagnostic',
  referralNotes: 'Suspected arrhythmia, needs ECG at provincial hospital',
  referralState: 'New',
  referralOutcome: null,
  createdAt: '2026-11-10T08:00:00Z',
  updatedAt: '2026-11-10T08:00:00Z',
  consultDate: '2026-11-10T07:45:00Z',
  doctorId: '01930000-0000-7000-8000-000000000099',
  doctorName: 'Dr. Lim',
  doctorNickname: 'Dr. Lim',
  referredToId: null,
  memberInChargeId: null,
  guardiansContacted: null,
  classOfService: null,
  descriptionOfService: null,
  patientSummaryFromScreening: null,
  remarksAtBooth: null,
  memberOnRunId: null,
  presentAtReferralParty: false,
  referralPartyResults: null,
  followUp: false,
  doctorsUpdated: false,
  amountPaidUsd: null,
  amountPaidRiels: null,
  memberWhoPaidId: null,
}

describe('referral constants & options', () => {
  it('defines referral states in lifecycle order', () => {
    expect(REFERRAL_STATES).toEqual([
      'New',
      'Seen',
      'Outgoing',
      'Completed',
      'CompletedSuccess',
      'CompletedFailure',
      'None',
    ])
  })

  it('maps every state to a human-readable label in options', () => {
    expect(REFERRAL_STATE_OPTIONS).toHaveLength(REFERRAL_STATES.length)
    for (const opt of REFERRAL_STATE_OPTIONS) {
      expect(opt.label).toBe(REFERRAL_STATE_LABELS[opt.value])
    }
    expect(REFERRAL_STATE_LABELS.CompletedSuccess).toBe('Completed (Success)')
    expect(REFERRAL_STATE_LABELS.CompletedFailure).toBe('Completed (Failure)')
  })

  it('defines referral categories including AdvancedVision label hint', () => {
    expect(REFERRAL_CATEGORIES).toContain('Not Referred')
    expect(REFERRAL_CATEGORIES).toContain('Diagnostic')
    expect(REFERRAL_CATEGORIES).toContain('Acute')
    expect(REFERRAL_CATEGORIES).toContain('Chronic')
    expect(REFERRAL_CATEGORIES).toContain('AdvancedVision')
    expect(REFERRAL_CATEGORIES).toContain('Others')

    const advancedVisionOpt = REFERRAL_CATEGORY_OPTIONS.find(o => o.value === 'AdvancedVision')
    expect(advancedVisionOpt?.label).toBe('AdvancedVision [Within clinic]')
  })

  it('returns valid badge classes for each state using clinical tokens', () => {
    for (const state of REFERRAL_STATES) {
      const cls = referralStateBadgeClass(state)
      expect(cls).toBeTypeOf('string')
      expect(cls.length).toBeGreaterThan(0)
    }
    expect(referralStateBadgeClass('New')).toContain('text-warning')
    expect(referralStateBadgeClass('Seen')).toContain('bg-accent')
    expect(referralStateBadgeClass('Outgoing')).toContain('text-chrome')
    expect(referralStateBadgeClass('Completed')).toContain('text-success')
    expect(referralStateBadgeClass('CompletedSuccess')).toContain('text-success')
    expect(referralStateBadgeClass('CompletedFailure')).toContain('text-destructive')
    expect(referralStateBadgeClass('None')).toContain('text-muted-foreground')
  })
})

describe('matchesReferral filtering', () => {
  it('matches all when no filter is applied', () => {
    expect(matchesReferral(sampleReferral, {})).toBe(true)
    expect(matchesReferral(sampleReferral, { search: '' })).toBe(true)
  })

  it('filters by patient name (case-insensitive)', () => {
    expect(matchesReferral(sampleReferral, { search: 'sokha' })).toBe(true)
    expect(matchesReferral(sampleReferral, { search: 'CHAN' })).toBe(true)
    expect(matchesReferral(sampleReferral, { search: 'Dara' })).toBe(false)
  })

  it('filters by formatted patient code', () => {
    expect(matchesReferral(sampleReferral, { search: 'SB0042' })).toBe(true)
    expect(matchesReferral(sampleReferral, { search: 'sb0042' })).toBe(true)
    expect(matchesReferral(sampleReferral, { search: 'SB0099' })).toBe(false)
  })

  it('filters by referred reason and notes', () => {
    expect(matchesReferral(sampleReferral, { search: 'Diagnostic' })).toBe(true)
    expect(matchesReferral(sampleReferral, { search: 'arrhythmia' })).toBe(true)
    expect(matchesReferral(sampleReferral, { search: 'provincial hospital' })).toBe(true)
    expect(matchesReferral(sampleReferral, { search: 'fracture' })).toBe(false)
  })

  it('filters by outcome when outcome is present', () => {
    const withOutcome = { ...sampleReferral, referralOutcome: 'Seen by cardiologist, prescribed beta-blocker' }
    expect(matchesReferral(withOutcome, { search: 'cardiologist' })).toBe(true)
    expect(matchesReferral(withOutcome, { search: 'beta-blocker' })).toBe(true)
  })

  it('filters by villageCodeId', () => {
    expect(matchesReferral(sampleReferral, { villageCodeId: '01930000-0000-7000-8000-000000000002' })).toBe(true)
    expect(matchesReferral(sampleReferral, { villageCodeId: '01930000-0000-7000-8000-000000000099' })).toBe(false)
  })

  it('filters by referralState and multiselect referralStates', () => {
    expect(matchesReferral(sampleReferral, { referralState: 'New' })).toBe(true)
    expect(matchesReferral(sampleReferral, { referralState: 'Completed' })).toBe(false)
    expect(matchesReferral(sampleReferral, { referralStates: ['New', 'Seen'] })).toBe(true)
    expect(matchesReferral(sampleReferral, { referralStates: ['Completed', 'Outgoing'] })).toBe(false)
  })

  it('filters by patient tags (any and all modes) and matches tags in search', () => {
    const withTags: ReferralListItem = { ...sampleReferral, tags: ['Diabetic', 'Follow-Up'] }
    expect(matchesReferral(withTags, { tags: ['diabetic', 'Pediatric'], tagMatchMode: 'any' })).toBe(true)
    expect(matchesReferral(withTags, { tags: ['diabetic', 'Pediatric'], tagMatchMode: 'all' })).toBe(false)
    expect(matchesReferral(withTags, { tags: ['diabetic', 'follow-up'], tagMatchMode: 'all' })).toBe(true)
    expect(matchesReferral(withTags, { search: 'Follow-Up' })).toBe(true)
  })

  it('supports Khmer numerals and zero-width spaces', () => {
    // ៤២ is 42 in Khmer numerals
    expect(matchesReferral(sampleReferral, { search: 'SB០0៤២' })).toBe(true)
    // with zero width space
    expect(matchesReferral(sampleReferral, { search: 'Sokha\u200BChan' })).toBe(true)
  })
})

import { calculateAge } from '../../app/utils/patient'
import {
  ADULT_AGE,
  GUARDIANS_CONTACTED_LABELS,
  MAX_RIELS,
  REFERRAL_SERVICE_CLASS_LABELS,
  USD_AMOUNT_PATTERN,
} from '../../app/utils/referral'

describe('referrals overhaul helpers', () => {
  it('computes age as of the referral date for minor/adult guardian check', () => {
    const dob = '2008-10-15T00:00:00Z'
    expect(calculateAge(dob, '2026-10-14T00:00:00Z')).toBe(17)
    expect(calculateAge(dob, '2026-10-15T00:00:00Z')).toBe(ADULT_AGE)
  })

  it('validates USD amounts up to 2 decimal places', () => {
    expect(USD_AMOUNT_PATTERN.test('0')).toBe(true)
    expect(USD_AMOUNT_PATTERN.test('25.50')).toBe(true)
    expect(USD_AMOUNT_PATTERN.test('99999999.99')).toBe(true)
    expect(USD_AMOUNT_PATTERN.test('12.345')).toBe(false)
    expect(USD_AMOUNT_PATTERN.test('-5')).toBe(false)
    expect(USD_AMOUNT_PATTERN.test('abc')).toBe(false)
  })

  it('exposes labels for service classes and guardians contacted and max riels', () => {
    expect(REFERRAL_SERVICE_CLASS_LABELS.ImagingDiagnostics).toBe('Imaging/Diagnostics')
    expect(GUARDIANS_CONTACTED_LABELS.NotApplicable).toBe('Not Applicable')
    expect(MAX_RIELS).toBe(2_147_483_647)
  })
})

describe('referral workflow form', () => {
  const blank = referralFormValues({ referralState: 'New' })
  const names = { partner: (id: string) => (id === 'p1' ? 'Kantha Bopha' : undefined), member: (id: string) => (id === 'u1' ? 'Sokha' : undefined) }

  it('fills blanks for anything not recorded', () => {
    expect(blank.status.referralState).toBe('New')
    expect(blank.booth.referredToId).toBe('')
    expect(blank.money.amountPaidUsd).toBe('')
    expect(blank.party.followUp).toBe(false)
    expect(referralFormValues({ amountPaidUsd: 12.5, amountPaidRiels: 4000 }).money).toMatchObject({ amountPaidUsd: '12.5', amountPaidRiels: '4000' })
  })

  it('lists only the parts that changed, in page order', () => {
    const edited = structuredClone(blank)
    edited.outcome.referralOutcome = 'Seen'
    edited.status.referralState = 'Seen'
    expect(changedReferralParts(edited, blank)).toEqual(['status', 'outcome'])
    expect(changedReferralParts(blank, structuredClone(blank))).toEqual([])
  })

  it('trims text, sends blanks as null and converts amounts', () => {
    const values = structuredClone(blank)
    values.booth.remarksAtBooth = '  call first  '
    values.booth.descriptionOfService = '   '
    values.money.amountPaidUsd = '12.50'
    expect(referralStepPayload('booth', values, { isAdult: false })).toMatchObject({
      remarksAtBooth: 'call first',
      descriptionOfService: null,
      referredToId: null,
      guardiansContacted: 'No',
    })
    expect(referralStepPayload('booth', values, { isAdult: true }).guardiansContacted).toBe('NotApplicable')
    expect(referralStepPayload('money', values, { isAdult: false })).toEqual({ amountPaidUsd: 12.5, amountPaidRiels: null, memberWhoPaidId: null })
  })

  it('validates payment amounts but allows blanks', () => {
    expect(validateReferralMoney(blank.money)).toEqual({})
    expect(validateReferralMoney({ ...blank.money, amountPaidUsd: '12.555' }).amountPaidUsd).toBeTruthy()
    expect(validateReferralMoney({ ...blank.money, amountPaidRiels: '12.5' }).amountPaidRiels).toBeTruthy()
    expect(validateReferralMoney({ ...blank.money, amountPaidUsd: '12.55', amountPaidRiels: '40000' })).toEqual({})
  })

  it('summarises each step', () => {
    const values = structuredClone(blank)
    expect(isReferralStepStarted('booth', values)).toBe(false)
    expect(referralStepSummary('booth', values, names)).toBe('Not recorded yet')
    values.booth.referredToId = 'p1'
    values.booth.classOfService = 'ChronicManagement'
    values.money.amountPaidRiels = '40000'
    values.party.presentAtReferralParty = true
    values.party.memberOnRunId = 'u1'
    expect(referralStepSummary('booth', values, names)).toBe('Kantha Bopha, Chronic Management')
    expect(referralStepSummary('money', values, names)).toBe('40,000 riels')
    expect(referralStepSummary('party', values, names)).toBe('Attended, Run by Sokha')
  })

  it('knows adults and status groups', () => {
    expect(isAdultAt('2000-01-01', '2026-01-01')).toBe(true)
    expect(isAdultAt('2015-01-01', '2026-01-01')).toBe(false)
    expect(isAdultAt(undefined, '2026-01-01')).toBe(false)
    expect(inReferralStatusGroup('CompletedFailure', 'Completed')).toBe(true)
    expect(inReferralStatusGroup('New', 'Completed')).toBe(false)
    expect(inReferralStatusGroup('None', null)).toBe(true)
  })
})

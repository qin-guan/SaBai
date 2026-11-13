import { describe, expect, it } from 'vitest'
import {
  matchesReferral,
  REFERRAL_CATEGORIES,
  REFERRAL_CATEGORY_OPTIONS,
  REFERRAL_STATE_LABELS,
  REFERRAL_STATE_OPTIONS,
  REFERRAL_STATES,
  referralStateBadgeClass,
  type ReferralListItem,
} from '~/utils/referral'

const sampleReferral: ReferralListItem = {
  id: 1,
  consultId: 10,
  doctorId: '00000000-0000-0000-0000-000000000001',
  patientId: 42,
  patientName: 'Sokha Chan',
  hasPhoto: true,
  villageCodeId: 2,
  villageCode: 'SB',
  villageColorHex: '#2563eb',
  referredFor: 'Diagnostic',
  referralNotes: 'Suspected arrhythmia, needs ECG at provincial hospital',
  referralState: 'New',
  referralOutcome: null,
  createdAt: '2026-11-10T08:00:00Z',
  consultDate: '2026-11-10T07:45:00Z',
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
    expect(matchesReferral(sampleReferral, { villageCodeId: 2 })).toBe(true)
    expect(matchesReferral(sampleReferral, { villageCodeId: 99 })).toBe(false)
  })

  it('filters by referralState', () => {
    expect(matchesReferral(sampleReferral, { referralState: 'New' })).toBe(true)
    expect(matchesReferral(sampleReferral, { referralState: 'Completed' })).toBe(false)
  })

  it('supports Khmer numerals and zero-width spaces', () => {
    // ៤២ is 42 in Khmer numerals
    expect(matchesReferral(sampleReferral, { search: 'SB០0៤២' })).toBe(true)
    // with zero width space
    expect(matchesReferral(sampleReferral, { search: 'Sokha\u200BChan' })).toBe(true)
  })
})

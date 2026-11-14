import { describe, expect, it } from 'vitest'
import { canRecordDisclosures, canViewDisclosures, currentConsents } from '~/composables/usePatientPrivacy'
import type { PatientConsent } from '~/composables/usePatientPrivacy'

const consent = (id: number, granted: boolean, recordedAt: string): PatientConsent => ({
  id,
  consentType: 'ClinicalDataSharing',
  granted,
  recordedAt,
  revokedAt: granted ? null : recordedAt,
  recordedByUserId: 'u',
  notes: null,
})

describe('currentConsents', () => {
  it('uses the most recent decision per type', () => {
    const map = currentConsents([
      consent(1, true, '2026-11-01T10:00:00Z'),
      consent(2, false, '2026-11-02T10:00:00Z'),
    ])
    expect(map.get('ClinicalDataSharing')?.granted).toBe(false)
  })

  it('breaks ties by id and leaves unrecorded types absent', () => {
    const map = currentConsents([
      consent(5, false, '2026-11-02T10:00:00Z'),
      consent(4, true, '2026-11-02T10:00:00Z'),
    ])
    expect(map.get('ClinicalDataSharing')?.id).toBe(5)
    expect(map.has('NoticeOfPrivacyPractices')).toBe(false)
  })
})

describe('disclosure roles', () => {
  it('mirrors the API role lists', () => {
    expect(canViewDisclosures(['auditor'])).toBe(true)
    expect(canViewDisclosures(['nurse'])).toBe(false)
    expect(canRecordDisclosures(['auditor'])).toBe(false)
    expect(canRecordDisclosures(['doctor'])).toBe(true)
  })
})

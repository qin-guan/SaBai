import { describe, expect, it } from 'vitest'
import { AUDIT_ACTION_LABELS, humanizeName } from '~/composables/useAuditLog'

describe('humanizeName', () => {
  it('humanizes PascalCase names', () => {
    expect(humanizeName('Patient')).toBe('Patient')
    expect(humanizeName('FirstName')).toBe('First Name')
    expect(humanizeName('DateOfBirth')).toBe('Date Of Birth')
    expect(humanizeName('EmergencyContactPhoneNumber')).toBe('Emergency Contact Phone Number')
  })

  it('handles acronyms in PascalCase names', () => {
    expect(humanizeName('StaffUserID')).toBe('Staff User ID')
    expect(humanizeName('IPAddress')).toBe('IP Address')
  })
})

describe('AUDIT_ACTION_LABELS', () => {
  it('has human-readable labels for all actions', () => {
    expect(AUDIT_ACTION_LABELS.Created).toBe('Created')
    expect(AUDIT_ACTION_LABELS.Updated).toBe('Updated')
    expect(AUDIT_ACTION_LABELS.Deleted).toBe('Deleted')
    expect(AUDIT_ACTION_LABELS.SignedIn).toBe('Signed in')
    expect(AUDIT_ACTION_LABELS.SignInFailed).toBe('Sign-in failed')
    expect(AUDIT_ACTION_LABELS.SignedOut).toBe('Signed out')
  })
})

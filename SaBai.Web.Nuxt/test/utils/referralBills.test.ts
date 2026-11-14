import { describe, expect, it } from 'vitest'
import type { PnLedgerEntryResponse } from '~/offline/mutations/referrals'
import type { ReferralListItem } from '~/utils/referral'
import {
  buildPnLedgerCsv,
  buildReferralBillsCsv,
  formatBillAmount,
  formatSignedBillAmount,
  hasReferralBill,
  isUnattributedBill,
  matchesReferralBill,
  summarizeReferralBills,
  UNASSIGNED_PAYER_KEY,
  type ReferralBillLookup,
} from '~/utils/referralBills'

function makeReferral(overrides: Partial<ReferralListItem> = {}): ReferralListItem {
  return {
    id: '01930000-0000-7000-8000-000000000001',
    consultId: '01930000-0000-7000-8000-000000000010',
    doctorId: '01930000-0000-7000-8000-000000000099',
    patientId: '01930000-0000-7000-8000-000000000042',
    patientName: 'Sokha Chan',
    patientDisplayId: 'SB0042',
    hasPhoto: false,
    villageCodeId: 'v-sb',
    villageCode: 'SB',
    villageColorHex: '#2563eb',
    referredFor: 'Diagnostic',
    referralNotes: null,
    referralState: 'Completed',
    referralOutcome: null,
    createdAt: '2026-11-10T08:00:00Z',
    consultDate: '2026-11-10T07:45:00Z',
    tags: [],
    referredToId: 'partner-1',
    amountPaidUsd: null,
    amountPaidRiels: null,
    memberWhoPaidId: null,
    ...overrides,
  }
}

const lookup: ReferralBillLookup = {
  memberName: id => ({ 'pn-1': 'Dara', 'pn-2': 'Vanna' }[id ?? '']),
  partnerName: id => ({ 'partner-1': 'Siem Reap Hospital', 'partner-2': 'Eye Clinic' }[id ?? '']),
}

describe('referralBills helpers', () => {
  it('identifies referrals with bills and unattributed bills', () => {
    expect(hasReferralBill(makeReferral())).toBe(false)
    expect(hasReferralBill(makeReferral({ amountPaidUsd: 0, amountPaidRiels: 0 }))).toBe(false)
    expect(hasReferralBill(makeReferral({ amountPaidUsd: 12.5 }))).toBe(true)
    expect(hasReferralBill(makeReferral({ amountPaidRiels: 40000 }))).toBe(true)
    expect(hasReferralBill(makeReferral({ memberWhoPaidId: 'pn-1' }))).toBe(true)

    expect(isUnattributedBill(makeReferral({ amountPaidUsd: 10 }))).toBe(true)
    expect(isUnattributedBill(makeReferral({ amountPaidUsd: 10, memberWhoPaidId: 'pn-1' }))).toBe(false)
    expect(isUnattributedBill(makeReferral({ memberWhoPaidId: 'pn-1' }))).toBe(false)
  })

  it('formats USD and KHR bill amounts and signed ledger amounts', () => {
    expect(formatBillAmount(null, null)).toBe('No amount')
    expect(formatBillAmount(12.5, null)).toBe('US$12.50')
    expect(formatBillAmount(null, 40000)).toBe('40,000 KHR')
    expect(formatBillAmount(12.5, 40000)).toBe('US$12.50 + 40,000 KHR')

    expect(formatSignedBillAmount(10, 0)).toBe('+US$10.00')
    expect(formatSignedBillAmount(-5.5, -10000)).toBe('-US$5.50 -10,000 KHR')
    expect(formatSignedBillAmount(0, 0)).toBe('US$0.00')
  })

  it('summarizes total and unattributed bills without float drift', () => {
    const bills = [
      makeReferral({ id: '1', amountPaidUsd: 10.1, amountPaidRiels: 20000, memberWhoPaidId: 'pn-1' }),
      makeReferral({ id: '2', amountPaidUsd: 5.2, memberWhoPaidId: 'pn-1' }),
      makeReferral({ id: '3', amountPaidUsd: 3.3, amountPaidRiels: 10000, memberWhoPaidId: null }),
    ]

    expect(summarizeReferralBills(bills)).toEqual({
      totalCount: 3,
      totalUsd: 18.6,
      totalRiels: 30000,
      unattributedCount: 1,
      unattributedUsd: 3.3,
      unattributedRiels: 10000,
    })
  })

  it('filters bills by PN member, unattributed flag, clinic, village, date range, and search', () => {
    const bill1 = makeReferral({
      id: '1',
      patientName: 'Sokha Chan',
      memberWhoPaidId: 'pn-1',
      referredToId: 'partner-1',
      villageCodeId: 'v-sb',
      createdAt: '2026-11-10T08:00:00Z',
      amountPaidUsd: 12,
    })
    const bill2 = makeReferral({
      id: '2',
      patientName: 'Dara Kim',
      memberWhoPaidId: null,
      referredToId: 'partner-2',
      villageCodeId: 'v-ot',
      createdAt: '2026-11-12T08:00:00Z',
      amountPaidUsd: 25,
    })

    expect(matchesReferralBill(bill1, { memberWhoPaidId: 'pn-1' }, lookup)).toBe(true)
    expect(matchesReferralBill(bill2, { memberWhoPaidId: 'pn-1' }, lookup)).toBe(false)
    expect(matchesReferralBill(bill2, { memberWhoPaidId: UNASSIGNED_PAYER_KEY }, lookup)).toBe(true)
    expect(matchesReferralBill(bill2, { unattributedOnly: true }, lookup)).toBe(true)
    expect(matchesReferralBill(bill1, { unattributedOnly: true }, lookup)).toBe(false)
    expect(matchesReferralBill(bill1, { referredToId: 'partner-1' }, lookup)).toBe(true)
    expect(matchesReferralBill(bill2, { dateFrom: '2026-11-11', dateTo: '2026-11-12' }, lookup)).toBe(true)
    expect(matchesReferralBill(bill1, { dateFrom: '2026-11-11' }, lookup)).toBe(false)
    expect(matchesReferralBill(bill2, { search: 'Eye Clinic' }, lookup)).toBe(true)
    expect(matchesReferralBill(bill1, { search: 'Dara' }, lookup)).toBe(true)
  })

  it('builds properly escaped CSV output for referral bills and double-entry ledger', () => {
    const bill = makeReferral({
      patientName: 'Chan, "Sokha"',
      amountPaidUsd: 12.5,
      amountPaidRiels: 40000,
      memberWhoPaidId: 'pn-1',
    })

    const csv = buildReferralBillsCsv([bill], lookup)
    const lines = csv.split('\r\n')
    expect(lines).toHaveLength(2)
    expect(lines[0]).toContain('PN Member Who Paid,Amount USD,Amount KHR')
    expect(lines[1]).toContain('"Chan, ""Sokha"""')
    expect(lines[1]).toContain('Siem Reap Hospital,Dara,12.50,40000')

    const entry: PnLedgerEntryResponse = {
      id: 'e1',
      memberId: 'pn-1',
      memberName: 'Dara',
      entryType: 'Repayment',
      debitAccount: 'PN:Payable',
      creditAccount: 'Clinic:Cash',
      amountUsd: -15,
      amountRiels: 0,
      referralId: null,
      patientName: null,
      reversedEntryId: null,
      isReversed: false,
      notes: 'Cash, EOD',
      createdAt: '2026-11-14T10:00:00Z',
      createdByUserId: 'u1',
      createdByName: 'Admin',
    }
    const ledgerCsv = buildPnLedgerCsv([entry])
    expect(ledgerCsv.split('\r\n')[1]).toContain('2026-11-14,Dara,Repayment,PN:Payable,Clinic:Cash,-15.00,0,,No,"Cash, EOD"')
  })
})

import { describe, expect, it } from 'vitest'
import type { ReferralListItem } from '~/utils/referral'
import {
  buildReferralBillsCsv,
  consolidateBillsByPn,
  formatBillAmount,
  getBillBreakdown,
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
    isReimbursed: false,
    reimbursedUsd: null,
    reimbursedRiels: null,
    reimbursedAt: null,
    reimbursedByUserId: null,
    reimbursementNotes: null,
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

  it('formats USD and KHR bill amounts', () => {
    expect(formatBillAmount(null, null)).toBe('No amount')
    expect(formatBillAmount(12.5, null)).toBe('US$12.50')
    expect(formatBillAmount(null, 40000)).toBe('40,000 KHR')
    expect(formatBillAmount(12.5, 40000)).toBe('US$12.50 + 40,000 KHR')
  })

  it('summarizes pending, reimbursed, and unattributed bills without float drift', () => {
    const bills = [
      makeReferral({ id: '1', amountPaidUsd: 10.1, amountPaidRiels: 20000, memberWhoPaidId: 'pn-1', isReimbursed: false }),
      makeReferral({ id: '2', amountPaidUsd: 5.2, memberWhoPaidId: 'pn-1', isReimbursed: true }),
      makeReferral({ id: '3', amountPaidUsd: 3.3, amountPaidRiels: 10000, memberWhoPaidId: null, isReimbursed: false }),
    ]

    const summary = summarizeReferralBills(bills)
    expect(summary).toEqual({
      totalCount: 3,
      totalUsd: 18.6,
      totalRiels: 30000,
      pendingCount: 2,
      pendingUsd: 13.4,
      pendingRiels: 30000,
      reimbursedCount: 1,
      reimbursedUsd: 5.2,
      reimbursedRiels: 0,
      unattributedCount: 1,
      unattributedUsd: 3.3,
      unattributedRiels: 10000,
    })
  })

  it('consolidates bills per PN member with unassigned first', () => {
    const bills = [
      makeReferral({ id: '1', amountPaidUsd: 10, memberWhoPaidId: 'pn-1', isReimbursed: false }),
      makeReferral({ id: '2', amountPaidUsd: 20, memberWhoPaidId: 'pn-1', isReimbursed: true }),
      makeReferral({ id: '3', amountPaidUsd: 5, memberWhoPaidId: 'pn-2', isReimbursed: false }),
      makeReferral({ id: '4', amountPaidRiels: 15000, memberWhoPaidId: null, isReimbursed: false }),
    ]

    const groups = consolidateBillsByPn(bills, id => lookup.memberName(id))
    expect(groups.map(g => g.memberId)).toEqual([UNASSIGNED_PAYER_KEY, 'pn-1', 'pn-2'])
    expect(groups[0]).toMatchObject({ isUnassigned: true, pendingCount: 1, pendingRiels: 15000, pendingIds: ['4'] })
    expect(groups[1]).toMatchObject({ memberName: 'Dara', totalCount: 2, pendingCount: 1, pendingUsd: 10, reimbursedUsd: 20, totalUsd: 30, pendingIds: ['1'] })
    expect(groups[2]).toMatchObject({ memberName: 'Vanna', totalCount: 1, pendingCount: 1, pendingUsd: 5, pendingIds: ['3'] })
  })

  it('filters bills by PN member, reimbursement status, clinic, village, date range, and search', () => {
    const bill1 = makeReferral({
      id: '1',
      patientName: 'Sokha Chan',
      memberWhoPaidId: 'pn-1',
      referredToId: 'partner-1',
      villageCodeId: 'v-sb',
      createdAt: '2026-11-10T08:00:00Z',
      amountPaidUsd: 12,
      isReimbursed: false,
    })
    const bill2 = makeReferral({
      id: '2',
      patientName: 'Dara Kim',
      memberWhoPaidId: 'pn-2',
      referredToId: 'partner-2',
      villageCodeId: 'v-ot',
      createdAt: '2026-11-12T08:00:00Z',
      amountPaidUsd: 25,
      isReimbursed: true,
      reimbursementNotes: 'Receipt 99',
    })

    expect(matchesReferralBill(bill1, { memberWhoPaidId: 'pn-1' }, lookup)).toBe(true)
    expect(matchesReferralBill(bill2, { memberWhoPaidId: 'pn-1' }, lookup)).toBe(false)
    expect(matchesReferralBill(bill1, { reimbursementStatus: 'pending' }, lookup)).toBe(true)
    expect(matchesReferralBill(bill2, { reimbursementStatus: 'pending' }, lookup)).toBe(false)
    expect(matchesReferralBill(bill2, { reimbursementStatus: 'reimbursed' }, lookup)).toBe(true)
    expect(matchesReferralBill(bill1, { referredToId: 'partner-1' }, lookup)).toBe(true)
    expect(matchesReferralBill(bill2, { dateFrom: '2026-11-11', dateTo: '2026-11-12' }, lookup)).toBe(true)
    expect(matchesReferralBill(bill1, { dateFrom: '2026-11-11' }, lookup)).toBe(false)
    expect(matchesReferralBill(bill2, { search: 'Eye Clinic' }, lookup)).toBe(true)
    expect(matchesReferralBill(bill2, { search: 'receipt 99' }, lookup)).toBe(true)
    expect(matchesReferralBill(bill1, { search: 'Dara' }, lookup)).toBe(true) // matches PN member Dara
  })

  it('builds properly escaped CSV output', () => {
    const bill = makeReferral({
      patientName: 'Chan, "Sokha"',
      amountPaidUsd: 12.5,
      amountPaidRiels: 40000,
      memberWhoPaidId: 'pn-1',
      isReimbursed: true,
      reimbursedAt: '2026-11-14T10:00:00Z',
      reimbursementNotes: 'Paid, cash',
    })

    const csv = buildReferralBillsCsv([bill], lookup)
    const lines = csv.split('\r\n')
    expect(lines).toHaveLength(2)
    expect(lines[0]).toContain('PN Member Who Paid,Amount USD,Amount KHR,Reimbursed USD,Reimbursed KHR,Remaining USD,Remaining KHR,Reimbursement Status')
    expect(lines[1]).toContain('"Chan, ""Sokha"""')
    expect(lines[1]).toContain('Siem Reap Hospital,Dara,12.50,40000,12.50,40000,0.00,0,Reimbursed,2026-11-14,"Paid, cash"')
  })

  it('computes partial repayment breakdowns, summaries, PN consolidation, and CSV status', () => {
    const partialBill = makeReferral({
      id: 'p1',
      amountPaidUsd: 20,
      amountPaidRiels: 40000,
      memberWhoPaidId: 'pn-1',
      isReimbursed: false,
      reimbursedUsd: 7.5,
      reimbursedRiels: 10000,
      reimbursedAt: '2026-11-14T10:00:00Z',
      reimbursementNotes: 'First installment',
    })

    expect(getBillBreakdown(partialBill)).toEqual({
      paidUsd: 20,
      paidRiels: 40000,
      reimbursedUsd: 7.5,
      reimbursedRiels: 10000,
      remainingUsd: 12.5,
      remainingRiels: 30000,
      isPartial: true,
    })

    const summary = summarizeReferralBills([partialBill])
    expect(summary).toMatchObject({
      totalCount: 1,
      totalUsd: 20,
      totalRiels: 40000,
      pendingCount: 1,
      pendingUsd: 12.5,
      pendingRiels: 30000,
      reimbursedCount: 0,
      reimbursedUsd: 7.5,
      reimbursedRiels: 10000,
    })

    const groups = consolidateBillsByPn([partialBill], id => lookup.memberName(id))
    expect(groups[0]).toMatchObject({
      memberId: 'pn-1',
      pendingCount: 1,
      pendingUsd: 12.5,
      pendingRiels: 30000,
      reimbursedUsd: 7.5,
      reimbursedRiels: 10000,
    })

    const csv = buildReferralBillsCsv([partialBill], lookup)
    expect(csv.split('\r\n')[1]).toContain('20.00,40000,7.50,10000,12.50,30000,Partial,2026-11-14,First installment')
  })
})

import { QueryClient } from '@tanstack/vue-query'
import { describe, expect, it } from 'vitest'
import {
  createReferral,
  deleteReferralPartner,
  recordPnRepayment,
  reversePnLedgerEntry,
  sortReferralPartners,
  updateReferralBooth,
  updateReferralPartner,
  updateReferralState,
  type PnLedgerData,
  type ReferralDetail,
  type ReferralListEntry,
  type ReferralPartner,
} from '~/offline/mutations/referrals'
import { queryKeys } from '~/utils/api'

const consultId = '01930000-0000-7000-8000-000000000010'
const referralId = '01930000-0000-7000-8000-000000000001'

const context = {
  patientId: '01930000-0000-7000-8000-000000000042',
  patientName: 'Sokha Chan',
  hasPhoto: false,
  villageCodeId: '01930000-0000-7000-8000-000000000002',
  villageCode: 'SB',
  villageColorHex: '#2563eb',
  doctorId: '01930000-0000-7000-8000-000000000099',
  doctorNickname: 'Lim',
  consultDate: '2026-11-10T07:45:00Z',
}

const createVars = { id: referralId, consultId, referredFor: 'Diagnostic', referralNotes: null, context }

describe('referral optimistic edits', () => {
  it('a referral made offline shows in the list, the consult list and opens', () => {
    const qc = new QueryClient()
    qc.setQueryData<ReferralListEntry[]>(queryKeys.referrals(), [])
    createReferral.optimistic!(createVars, qc)
    createReferral.optimistic!(createVars, qc)

    expect(qc.getQueryData<ReferralListEntry[]>(queryKeys.referrals())).toHaveLength(1)
    expect(qc.getQueryData<ReferralListEntry[]>(queryKeys.referrals())![0]).toMatchObject({ id: referralId, patientName: 'Sokha Chan', referralState: 'New' })
    expect(qc.getQueryData(queryKeys.referralsByConsult(consultId))).toHaveLength(1)
    expect(qc.getQueryData<ReferralDetail>(queryKeys.referral(referralId))).toMatchObject({ doctorNickname: 'Lim', followUp: false })
  })

  it('without context only the consult list is edited', () => {
    const qc = new QueryClient()
    createReferral.optimistic!({ ...createVars, context: undefined }, qc)
    expect(qc.getQueryData(queryKeys.referralsByConsult(consultId))).toHaveLength(1)
    expect(qc.getQueryData(queryKeys.referrals())).toBeUndefined()
    expect(qc.getQueryData(queryKeys.referral(referralId))).toBeUndefined()
  })

  it('a status change reaches the detail, the list and the consult list without touching the old objects', () => {
    const qc = new QueryClient()
    qc.setQueryData<ReferralListEntry[]>(queryKeys.referrals(), [])
    createReferral.optimistic!(createVars, qc)
    const before = qc.getQueryData<ReferralListEntry[]>(queryKeys.referrals())!
    updateReferralState.optimistic!({ id: referralId, referralState: 'Seen' }, qc)
    updateReferralState.optimistic!({ id: referralId, referralState: 'Seen' }, qc)

    expect(before[0]!.referralState).toBe('New')
    expect(qc.getQueryData<ReferralListEntry[]>(queryKeys.referrals())![0]!.referralState).toBe('Seen')
    expect(qc.getQueryData<ReferralDetail>(queryKeys.referral(referralId))!.referralState).toBe('Seen')
    expect(qc.getQueryData<ReferralListEntry[]>(queryKeys.referralsByConsult(consultId))![0]!.referralState).toBe('Seen')
  })

  it('a booth edit sets the booth fields on the detail only', () => {
    const qc = new QueryClient()
    createReferral.optimistic!(createVars, qc)
    updateReferralBooth.optimistic!({
      id: referralId,
      referredToId: 'p1',
      memberInChargeId: null,
      guardiansContacted: 'No',
      classOfService: 'ChronicManagement',
      descriptionOfService: 'Echo',
      patientSummaryFromScreening: null,
      remarksAtBooth: null,
    }, qc)
    expect(qc.getQueryData<ReferralDetail>(queryKeys.referral(referralId))).toMatchObject({ referredToId: 'p1', classOfService: 'ChronicManagement' })
    expect(updateReferralBooth.recordIds!({ id: referralId, referredToId: 'p1' } as never)).toEqual([referralId])
    expect(updateReferralBooth.dependsOn!({ id: referralId, referredToId: 'p1' } as never)).toEqual(['p1'])
  })

  it('records PN repayments (including overpayment) and reversals optimistically in the double-entry ledger', () => {
    const qc = new QueryClient()
    qc.setQueryData<PnLedgerData>(queryKeys.pnLedger(), {
      balances: [
        {
          memberId: 'pn-1',
          memberName: 'Dara',
          totalBillsUsd: 20,
          totalBillsRiels: 0,
          totalRepaidUsd: 0,
          totalRepaidRiels: 0,
          totalAdjustmentsUsd: 0,
          totalAdjustmentsRiels: 0,
          balanceUsd: 20,
          balanceRiels: 0,
          entryCount: 1,
          lastActivityAt: '2026-11-10T08:00:00Z',
        },
      ],
      entries: [
        {
          id: 'bill-1',
          memberId: 'pn-1',
          memberName: 'Dara',
          entryType: 'Bill',
          debitAccount: 'Clinic:ReferralExpense',
          creditAccount: 'PN:Payable',
          amountUsd: 20,
          amountRiels: 0,
          referralId: referralId,
          patientName: 'Sokha Chan',
          reversedEntryId: null,
          isReversed: false,
          notes: 'Referral bill recorded',
          createdAt: '2026-11-10T08:00:00Z',
          createdByUserId: null,
          createdByName: null,
        },
      ],
    })

    // Overpay $25 when $20 is owed -> balance becomes -$5
    recordPnRepayment.optimistic!({
      memberId: 'pn-1',
      memberName: 'Dara',
      amountUsd: 25,
      amountRiels: null,
      notes: 'Cash',
    }, qc)

    const afterRepay = qc.getQueryData<PnLedgerData>(queryKeys.pnLedger())!
    expect(afterRepay.balances[0]).toMatchObject({
      memberId: 'pn-1',
      totalRepaidUsd: 25,
      balanceUsd: -5,
    })
    const repayEntry = afterRepay.entries[0]!
    expect(repayEntry).toMatchObject({
      entryType: 'Repayment',
      debitAccount: 'PN:Payable',
      creditAccount: 'Clinic:Cash',
      amountUsd: -25,
    })

    // Reverse that repayment -> balance returns to +$20
    reversePnLedgerEntry.optimistic!({ id: repayEntry.id, notes: 'Typo' }, qc)
    const afterReverse = qc.getQueryData<PnLedgerData>(queryKeys.pnLedger())!
    expect(afterReverse.balances[0]).toMatchObject({
      memberId: 'pn-1',
      totalAdjustmentsUsd: 25,
      balanceUsd: 20,
    })
    expect(afterReverse.entries[0]).toMatchObject({
      entryType: 'Adjustment',
      debitAccount: 'Clinic:Cash',
      creditAccount: 'PN:Payable',
      amountUsd: 25,
      reversedEntryId: repayEntry.id,
    })
    expect(afterReverse.entries.find(e => e.id === repayEntry.id)?.isReversed).toBe(true)
  })
})

describe('referral partner optimistic edits', () => {
  const partner = (name: string, isActive: boolean, id = name): ReferralPartner => ({ id, name, remarks: null, isActive, createdAt: 'then', updatedAt: 'then' })

  it('lists active partners first, then by name', () => {
    expect(sortReferralPartners([partner('B', true), partner('A', false), partner('A2', true)]).map(p => p.name)).toEqual(['A2', 'B', 'A'])
  })

  it('an edit keeps createdAt and re-sorts; a delete removes by id', () => {
    const qc = new QueryClient()
    qc.setQueryData<ReferralPartner[]>(queryKeys.referralPartners(), [partner('A', true), partner('B', true)])
    updateReferralPartner.optimistic!({ id: 'A', name: 'A', remarks: 'x', isActive: false }, qc)
    updateReferralPartner.optimistic!({ id: 'A', name: 'A', remarks: 'x', isActive: false }, qc)
    const edited = qc.getQueryData<ReferralPartner[]>(queryKeys.referralPartners())!
    expect(edited.map(p => p.id)).toEqual(['B', 'A'])
    expect(edited[1]).toMatchObject({ createdAt: 'then', remarks: 'x', isActive: false })

    deleteReferralPartner.optimistic!({ id: 'B', name: 'B' }, qc)
    expect(qc.getQueryData<ReferralPartner[]>(queryKeys.referralPartners())!.map(p => p.id)).toEqual(['A'])
  })
})

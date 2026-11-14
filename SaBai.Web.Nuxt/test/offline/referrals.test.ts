import { QueryClient } from '@tanstack/vue-query'
import { describe, expect, it } from 'vitest'
import {
  createReferral,
  deleteReferralPartner,
  sortReferralPartners,
  updateReferralBooth,
  updateReferralPartner,
  updateReferralState,
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
    expect(updateReferralBooth.recordIds!({ id: referralId, referredToId: 'p1' } as never)).toEqual([referralId, 'p1'])
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

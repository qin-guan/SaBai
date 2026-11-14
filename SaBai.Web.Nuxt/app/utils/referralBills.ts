import { cleanKhmerText, matchesSearchText } from './khmer'
import type { ReferralListItem } from './referral'

export type ReimbursementStatusFilter = 'all' | 'pending' | 'reimbursed' | 'unattributed'

export const UNASSIGNED_PAYER_KEY = '__unassigned__'

export interface ReferralBillSummary {
  totalCount: number
  totalUsd: number
  totalRiels: number
  pendingCount: number
  pendingUsd: number
  pendingRiels: number
  reimbursedCount: number
  reimbursedUsd: number
  reimbursedRiels: number
  unattributedCount: number
  unattributedUsd: number
  unattributedRiels: number
}

export interface PnMemberBillGroup {
  memberId: string
  memberName: string
  isUnassigned: boolean
  totalCount: number
  pendingCount: number
  reimbursedCount: number
  pendingUsd: number
  pendingRiels: number
  reimbursedUsd: number
  reimbursedRiels: number
  totalUsd: number
  totalRiels: number
  pendingIds: string[]
}

export interface ReferralBillFilterOptions {
  search?: string
  memberWhoPaidId?: string | null
  reimbursementStatus?: ReimbursementStatusFilter
  referredToId?: string | null
  villageCodeId?: string | null
  dateFrom?: string | null
  dateTo?: string | null
}

export interface ReferralBillLookup {
  memberName: (id: string | null | undefined) => string | undefined
  partnerName: (id: string | null | undefined) => string | undefined
}

/** Whether a referral has a recorded payment amount or an assigned payer. */
export function hasReferralBill(item: Pick<ReferralListItem, 'amountPaidUsd' | 'amountPaidRiels' | 'memberWhoPaidId'>): boolean {
  return (item.amountPaidUsd ?? 0) > 0 || (item.amountPaidRiels ?? 0) > 0 || !!item.memberWhoPaidId
}

/** Whether a referral has a monetary amount recorded without a PN member assigned. */
export function isUnattributedBill(item: Pick<ReferralListItem, 'amountPaidUsd' | 'amountPaidRiels' | 'memberWhoPaidId'>): boolean {
  return ((item.amountPaidUsd ?? 0) > 0 || (item.amountPaidRiels ?? 0) > 0) && !item.memberWhoPaidId
}

export function formatUsd(amount: number): string {
  return `US$${amount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

export function formatRiels(amount: number): string {
  return `${Math.round(amount).toLocaleString('en-US')} KHR`
}

export function formatBillAmount(usd: number | null | undefined, riels: number | null | undefined): string {
  const parts: string[] = []
  if (usd != null && usd > 0)
    parts.push(formatUsd(usd))
  if (riels != null && riels > 0)
    parts.push(formatRiels(riels))
  return parts.length ? parts.join(' + ') : 'No amount'
}

/** Aggregates bill counts and currency totals across a list of referral bills. */
export function summarizeReferralBills(items: readonly ReferralListItem[]): ReferralBillSummary {
  const summary: ReferralBillSummary = {
    totalCount: 0,
    totalUsd: 0,
    totalRiels: 0,
    pendingCount: 0,
    pendingUsd: 0,
    pendingRiels: 0,
    reimbursedCount: 0,
    reimbursedUsd: 0,
    reimbursedRiels: 0,
    unattributedCount: 0,
    unattributedUsd: 0,
    unattributedRiels: 0,
  }

  for (const item of items) {
    const usd = item.amountPaidUsd ?? 0
    const riels = item.amountPaidRiels ?? 0
    summary.totalCount += 1
    summary.totalUsd += usd
    summary.totalRiels += riels

    if (item.isReimbursed) {
      summary.reimbursedCount += 1
      summary.reimbursedUsd += usd
      summary.reimbursedRiels += riels
    }
    else {
      summary.pendingCount += 1
      summary.pendingUsd += usd
      summary.pendingRiels += riels
    }

    if (isUnattributedBill(item)) {
      summary.unattributedCount += 1
      summary.unattributedUsd += usd
      summary.unattributedRiels += riels
    }
  }

  // Avoid floating-point drift on USD sums.
  summary.totalUsd = Math.round(summary.totalUsd * 100) / 100
  summary.pendingUsd = Math.round(summary.pendingUsd * 100) / 100
  summary.reimbursedUsd = Math.round(summary.reimbursedUsd * 100) / 100
  summary.unattributedUsd = Math.round(summary.unattributedUsd * 100) / 100

  return summary
}

/** Consolidates bills per PN member (plus an Unassigned group if any bill lacks a payer). */
export function consolidateBillsByPn(
  items: readonly ReferralListItem[],
  memberName: (id: string) => string | undefined,
): PnMemberBillGroup[] {
  const groups = new Map<string, PnMemberBillGroup>()

  for (const item of items) {
    const isUnassigned = !item.memberWhoPaidId
    const key = item.memberWhoPaidId ?? UNASSIGNED_PAYER_KEY
    let group = groups.get(key)
    if (!group) {
      group = {
        memberId: key,
        memberName: isUnassigned ? 'Unassigned (missing payer)' : (memberName(key) ?? 'Unknown PN member'),
        isUnassigned,
        totalCount: 0,
        pendingCount: 0,
        reimbursedCount: 0,
        pendingUsd: 0,
        pendingRiels: 0,
        reimbursedUsd: 0,
        reimbursedRiels: 0,
        totalUsd: 0,
        totalRiels: 0,
        pendingIds: [],
      }
      groups.set(key, group)
    }

    const usd = item.amountPaidUsd ?? 0
    const riels = item.amountPaidRiels ?? 0
    group.totalCount += 1
    group.totalUsd += usd
    group.totalRiels += riels

    if (item.isReimbursed) {
      group.reimbursedCount += 1
      group.reimbursedUsd += usd
      group.reimbursedRiels += riels
    }
    else {
      group.pendingCount += 1
      group.pendingUsd += usd
      group.pendingRiels += riels
      group.pendingIds.push(item.id)
    }
  }

  for (const g of groups.values()) {
    g.totalUsd = Math.round(g.totalUsd * 100) / 100
    g.pendingUsd = Math.round(g.pendingUsd * 100) / 100
    g.reimbursedUsd = Math.round(g.reimbursedUsd * 100) / 100
  }

  return [...groups.values()].sort((a, b) => {
    if (a.isUnassigned !== b.isUnassigned)
      return a.isUnassigned ? -1 : 1
    if (a.pendingCount !== b.pendingCount)
      return b.pendingCount - a.pendingCount
    if (a.totalUsd !== b.totalUsd)
      return b.totalUsd - a.totalUsd
    return a.memberName.localeCompare(b.memberName)
  })
}

/** Filters a referral bill by PN member, reimbursement status, partner, village, date range, and text search. */
export function matchesReferralBill(
  item: ReferralListItem,
  filters: ReferralBillFilterOptions,
  lookup: ReferralBillLookup,
): boolean {
  if (filters.memberWhoPaidId) {
    if (filters.memberWhoPaidId === UNASSIGNED_PAYER_KEY) {
      if (item.memberWhoPaidId)
        return false
    }
    else if (item.memberWhoPaidId !== filters.memberWhoPaidId) {
      return false
    }
  }

  if (filters.reimbursementStatus === 'pending' && item.isReimbursed)
    return false
  if (filters.reimbursementStatus === 'reimbursed' && !item.isReimbursed)
    return false
  if (filters.reimbursementStatus === 'unattributed' && !isUnattributedBill(item))
    return false

  if (filters.referredToId && item.referredToId !== filters.referredToId)
    return false

  if (filters.villageCodeId && item.villageCodeId !== filters.villageCodeId)
    return false

  if (filters.dateFrom || filters.dateTo) {
    const day = item.createdAt.slice(0, 10)
    if (filters.dateFrom && day < filters.dateFrom)
      return false
    if (filters.dateTo && day > filters.dateTo)
      return false
  }

  const needle = cleanKhmerText(filters.search ?? '')
  if (!needle)
    return true

  const targets = [
    item.patientName,
    item.patientDisplayId ?? '',
    item.villageCode,
    item.referredFor,
    item.referralNotes ?? '',
    item.reimbursementNotes ?? '',
    lookup.partnerName(item.referredToId) ?? '',
    lookup.memberName(item.memberWhoPaidId) ?? '',
  ]

  return targets.some(target => matchesSearchText(target, needle))
}

function csvCell(value: string | number | null | undefined): string {
  const str = value == null ? '' : String(value)
  if (/[",\n\r]/.test(str))
    return `"${str.replace(/"/g, '""')}"`
  return str
}

/** Builds an RFC 4180 CSV export of the given referral bills. */
export function buildReferralBillsCsv(items: readonly ReferralListItem[], lookup: ReferralBillLookup): string {
  const headers = [
    'Referral Date',
    'Patient Name',
    'Patient ID',
    'Village',
    'Referred For',
    'Partner Clinic',
    'PN Member Who Paid',
    'Amount USD',
    'Amount KHR',
    'Reimbursement Status',
    'Reimbursed At',
    'Reimbursement Notes',
  ]

  const rows = items.map(item => [
    item.createdAt.slice(0, 10),
    item.patientName,
    item.patientDisplayId ?? '',
    item.villageCode,
    item.referredFor,
    lookup.partnerName(item.referredToId) ?? '',
    item.memberWhoPaidId ? (lookup.memberName(item.memberWhoPaidId) ?? '') : 'Unassigned',
    item.amountPaidUsd != null ? item.amountPaidUsd.toFixed(2) : '',
    item.amountPaidRiels != null ? String(item.amountPaidRiels) : '',
    item.isReimbursed ? 'Reimbursed' : 'Pending',
    item.reimbursedAt ? item.reimbursedAt.slice(0, 10) : '',
    item.reimbursementNotes ?? '',
  ])

  return [headers, ...rows].map(row => row.map(csvCell).join(',')).join('\r\n')
}

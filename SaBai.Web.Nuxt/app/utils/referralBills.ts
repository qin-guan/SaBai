import type { PnLedgerEntryResponse } from '@sabai/sdk/types'
import { cleanKhmerText, matchesSearchText } from './khmer'
import type { ReferralListItem } from './referral'

export const UNASSIGNED_PAYER_KEY = '__unassigned__'

export interface ReferralBillSummary {
  totalCount: number
  totalUsd: number
  totalRiels: number
  unattributedCount: number
  unattributedUsd: number
  unattributedRiels: number
}

export interface ReferralBillFilterOptions {
  search?: string
  memberWhoPaidId?: string | null
  unattributedOnly?: boolean
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
  if (usd != null && usd !== 0)
    parts.push(formatUsd(usd))
  if (riels != null && riels !== 0)
    parts.push(formatRiels(riels))
  return parts.length ? parts.join(' + ') : 'No amount'
}

export function formatSignedBillAmount(usd: number, riels: number): string {
  const parts: string[] = []
  if (usd !== 0)
    parts.push(`${usd > 0 ? '+' : '-'}${formatUsd(Math.abs(usd))}`)
  if (riels !== 0)
    parts.push(`${riels > 0 ? '+' : '-'}${formatRiels(Math.abs(riels))}`)
  return parts.length ? parts.join(' ') : 'US$0.00'
}

function addUsdCents(cents: number, usd: number | null | undefined): number {
  return cents + Math.round((usd ?? 0) * 100)
}

/** Aggregates bill counts and currency totals across referral bills. */
export function summarizeReferralBills(items: readonly ReferralListItem[]): ReferralBillSummary {
  let totalUsdCents = 0
  let totalRiels = 0
  let unattributedCount = 0
  let unattributedUsdCents = 0
  let unattributedRiels = 0

  for (const item of items) {
    totalUsdCents = addUsdCents(totalUsdCents, item.amountPaidUsd)
    totalRiels += item.amountPaidRiels ?? 0
    if (isUnattributedBill(item)) {
      unattributedCount++
      unattributedUsdCents = addUsdCents(unattributedUsdCents, item.amountPaidUsd)
      unattributedRiels += item.amountPaidRiels ?? 0
    }
  }

  return {
    totalCount: items.length,
    totalUsd: totalUsdCents / 100,
    totalRiels,
    unattributedCount,
    unattributedUsd: unattributedUsdCents / 100,
    unattributedRiels,
  }
}

/** Filters referral bills by PN member, clinic, village, date range, and search text. */
export function matchesReferralBill(
  item: ReferralListItem,
  options: ReferralBillFilterOptions,
  lookup: ReferralBillLookup,
): boolean {
  if (options.unattributedOnly && !isUnattributedBill(item))
    return false

  if (options.memberWhoPaidId) {
    if (options.memberWhoPaidId === UNASSIGNED_PAYER_KEY) {
      if (item.memberWhoPaidId)
        return false
    }
    else if (item.memberWhoPaidId !== options.memberWhoPaidId) {
      return false
    }
  }

  if (options.referredToId && item.referredToId !== options.referredToId)
    return false

  if (options.villageCodeId && item.villageCodeId !== options.villageCodeId)
    return false

  const isoDay = item.createdAt.slice(0, 10)
  if (options.dateFrom && isoDay < options.dateFrom)
    return false
  if (options.dateTo && isoDay > options.dateTo)
    return false

  const q = cleanKhmerText(options.search ?? '')
  if (!q)
    return true

  const pnName = lookup.memberName(item.memberWhoPaidId) ?? ''
  const clinicName = lookup.partnerName(item.referredToId) ?? ''
  const haystack = [
    item.patientName,
    item.patientDisplayId,
    item.villageCode,
    item.referredFor,
    pnName,
    clinicName,
  ].filter(Boolean).join(' ')
  return matchesSearchText(haystack, q)
}

function escapeCsvCell(value: string): string {
  if (value.includes(',') || value.includes('"') || value.includes('\n') || value.includes('\r'))
    return `"${value.replaceAll('"', '""')}"`
  return value
}

/** Builds an RFC-4180 CSV export of the given referral bills. */
export function buildReferralBillsCsv(items: readonly ReferralListItem[], lookup: ReferralBillLookup): string {
  const header = [
    'Referral Date',
    'Patient Name',
    'Patient ID',
    'Village',
    'Referred For',
    'Partner Clinic',
    'PN Member Who Paid',
    'Amount USD',
    'Amount KHR',
  ]

  const rows = items.map((item) => {
    const paidUsd = item.amountPaidUsd ?? 0
    const paidRiels = item.amountPaidRiels ?? 0
    return [
      item.createdAt.slice(0, 10),
      item.patientName,
      item.patientDisplayId ?? '',
      item.villageCode,
      item.referredFor,
      lookup.partnerName(item.referredToId) ?? '',
      lookup.memberName(item.memberWhoPaidId) ?? 'Unassigned',
      paidUsd > 0 ? paidUsd.toFixed(2) : '',
      paidRiels > 0 ? String(paidRiels) : '',
    ].map(escapeCsvCell).join(',')
  })

  return [header.join(','), ...rows].join('\r\n')
}

/** Builds an RFC-4180 CSV export of double-entry PN ledger entries. */
export function buildPnLedgerCsv(entries: readonly PnLedgerEntryResponse[]): string {
  const header = [
    'Date',
    'PN Member',
    'Type',
    'Debit Account',
    'Credit Account',
    'Amount USD',
    'Amount KHR',
    'Patient',
    'Reversed',
    'Notes',
  ]

  const rows = entries.map(e => [
    e.createdAt.slice(0, 10),
    e.memberName,
    e.entryType,
    e.debitAccount,
    e.creditAccount,
    e.amountUsd.toFixed(2),
    String(e.amountRiels),
    e.patientName ?? '',
    e.isReversed ? 'Yes' : 'No',
    e.notes ?? '',
  ].map(escapeCsvCell).join(','))

  return [header.join(','), ...rows].join('\r\n')
}

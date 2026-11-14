export type PnLedgerEntryType = 'Bill' | 'Repayment' | 'Adjustment'

export type PnMemberBalanceResponse = {
  memberId: string
  memberName: string
  totalBillsUsd: number
  totalBillsRiels: number
  totalRepaidUsd: number
  totalRepaidRiels: number
  totalAdjustmentsUsd: number
  totalAdjustmentsRiels: number
  balanceUsd: number
  balanceRiels: number
  entryCount: number
  lastActivityAt: string | null
}

export type PnLedgerEntryResponse = {
  id: string
  memberId: string
  memberName: string
  entryType: PnLedgerEntryType
  debitAccount: string
  creditAccount: string
  amountUsd: number
  amountRiels: number
  referralId: string | null
  patientName: string | null
  reversedEntryId: string | null
  isReversed: boolean
  notes: string | null
  createdAt: string
  createdByUserId: string | null
  createdByName: string | null
}

export type SaBaiAPIEndpointsPnLedgerListGetResponse = {
  balances: PnMemberBalanceResponse[]
  entries: PnLedgerEntryResponse[]
}

export type SaBaiAPIEndpointsPnLedgerListGetEndpointStatus200 = SaBaiAPIEndpointsPnLedgerListGetResponse
export type SaBaiAPIEndpointsPnLedgerListGetEndpointStatus401 = unknown
export type SaBaiAPIEndpointsPnLedgerListGetEndpointStatus403 = unknown
export type SaBaiAPIEndpointsPnLedgerListGetEndpointResponse = SaBaiAPIEndpointsPnLedgerListGetEndpointStatus200

export type SaBaiAPIEndpointsPnLedgerListGetEndpointResponses = {
  '200': SaBaiAPIEndpointsPnLedgerListGetEndpointStatus200
  '401': SaBaiAPIEndpointsPnLedgerListGetEndpointStatus401
  '403': SaBaiAPIEndpointsPnLedgerListGetEndpointStatus403
}

export type SaBaiAPIEndpointsPnLedgerListGetEndpointOptions = {
  body?: never
  path?: never
  query?: never
  headers?: never
}

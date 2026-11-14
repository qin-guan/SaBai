export type SaBaiAPIEndpointsPnLedgerReversePostRequest = {
  id: string
  notes?: string | null
}

export type SaBaiAPIEndpointsPnLedgerReversePostResponse = {
  id: string
  reversedEntryId: string
  memberId: string
  balanceUsd: number
  balanceRiels: number
  createdAt: string
}

export type SaBaiAPIEndpointsPnLedgerReversePostEndpointPath = {
  Id: string
}

export type SaBaiAPIEndpointsPnLedgerReversePostEndpointBody = SaBaiAPIEndpointsPnLedgerReversePostRequest
export type SaBaiAPIEndpointsPnLedgerReversePostEndpointStatus200 = SaBaiAPIEndpointsPnLedgerReversePostResponse
export type SaBaiAPIEndpointsPnLedgerReversePostEndpointStatus400 = unknown
export type SaBaiAPIEndpointsPnLedgerReversePostEndpointStatus401 = unknown
export type SaBaiAPIEndpointsPnLedgerReversePostEndpointStatus403 = unknown
export type SaBaiAPIEndpointsPnLedgerReversePostEndpointResponse = SaBaiAPIEndpointsPnLedgerReversePostEndpointStatus200

export type SaBaiAPIEndpointsPnLedgerReversePostEndpointResponses = {
  '200': SaBaiAPIEndpointsPnLedgerReversePostEndpointStatus200
  '400': SaBaiAPIEndpointsPnLedgerReversePostEndpointStatus400
  '401': SaBaiAPIEndpointsPnLedgerReversePostEndpointStatus401
  '403': SaBaiAPIEndpointsPnLedgerReversePostEndpointStatus403
}

export type SaBaiAPIEndpointsPnLedgerReversePostEndpointOptions = {
  body: SaBaiAPIEndpointsPnLedgerReversePostEndpointBody
  path: SaBaiAPIEndpointsPnLedgerReversePostEndpointPath
  query?: never
  headers?: never
}

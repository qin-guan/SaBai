export type SaBaiAPIEndpointsPnLedgerRepayPostRequest = {
  memberId: string
  amountUsd?: number | null
  amountRiels?: number | null
  notes?: string | null
}

export type SaBaiAPIEndpointsPnLedgerRepayPostResponse = {
  id: string
  memberId: string
  balanceUsd: number
  balanceRiels: number
  createdAt: string
}

export type SaBaiAPIEndpointsPnLedgerRepayPostEndpointBody = SaBaiAPIEndpointsPnLedgerRepayPostRequest
export type SaBaiAPIEndpointsPnLedgerRepayPostEndpointStatus200 = SaBaiAPIEndpointsPnLedgerRepayPostResponse
export type SaBaiAPIEndpointsPnLedgerRepayPostEndpointStatus400 = unknown
export type SaBaiAPIEndpointsPnLedgerRepayPostEndpointStatus401 = unknown
export type SaBaiAPIEndpointsPnLedgerRepayPostEndpointStatus403 = unknown
export type SaBaiAPIEndpointsPnLedgerRepayPostEndpointResponse = SaBaiAPIEndpointsPnLedgerRepayPostEndpointStatus200

export type SaBaiAPIEndpointsPnLedgerRepayPostEndpointResponses = {
  '200': SaBaiAPIEndpointsPnLedgerRepayPostEndpointStatus200
  '400': SaBaiAPIEndpointsPnLedgerRepayPostEndpointStatus400
  '401': SaBaiAPIEndpointsPnLedgerRepayPostEndpointStatus401
  '403': SaBaiAPIEndpointsPnLedgerRepayPostEndpointStatus403
}

export type SaBaiAPIEndpointsPnLedgerRepayPostEndpointOptions = {
  body: SaBaiAPIEndpointsPnLedgerRepayPostEndpointBody
  path?: never
  query?: never
  headers?: never
}

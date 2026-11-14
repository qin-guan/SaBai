import type { Options, Unwrappable, RequestResult } from '../.kubb/client'
import type { SaBaiAPIEndpointsPnLedgerRepayPostEndpointOptions, SaBaiAPIEndpointsPnLedgerRepayPostEndpointResponses } from '../types/SaBaiAPIEndpointsPnLedgerRepayPostEndpoint'
import { client, withUnwrap } from '../.kubb/client'

/**
 * @summary Record a repayment to a PN member (supports underpayment and overpayment)
 * {@link /pn-ledger/repayments}
 */
export function saBaiAPIEndpointsPnLedgerRepayPostEndpoint<ThrowOnError extends boolean = true>(options: Options<SaBaiAPIEndpointsPnLedgerRepayPostEndpointOptions, ThrowOnError>): Unwrappable<RequestResult<SaBaiAPIEndpointsPnLedgerRepayPostEndpointResponses, ThrowOnError>> {
  const { client: request = client, ...config } = options

  return withUnwrap(request({ method: 'POST', url: '/pn-ledger/repayments', security: [{ type: 'apiKey', name: 'sabai.auth', in: 'cookie', scopes: ['admin', 'member', 'pn'] }], contentType: { request: 'application/json' }, ...config, throwOnError: config.throwOnError ?? true }) as Promise<RequestResult<SaBaiAPIEndpointsPnLedgerRepayPostEndpointResponses, ThrowOnError>>)
}

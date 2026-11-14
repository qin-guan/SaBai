import type { Options, Unwrappable, RequestResult } from '../.kubb/client'
import type { SaBaiAPIEndpointsPnLedgerReversePostEndpointOptions, SaBaiAPIEndpointsPnLedgerReversePostEndpointResponses } from '../types/SaBaiAPIEndpointsPnLedgerReversePostEndpoint'
import { client, withUnwrap } from '../.kubb/client'

/**
 * @summary Reverse a PN repayment or adjustment ledger entry
 * {@link /pn-ledger/entries/:Id/reverse}
 */
export function saBaiAPIEndpointsPnLedgerReversePostEndpoint<ThrowOnError extends boolean = true>(options: Options<SaBaiAPIEndpointsPnLedgerReversePostEndpointOptions, ThrowOnError>): Unwrappable<RequestResult<SaBaiAPIEndpointsPnLedgerReversePostEndpointResponses, ThrowOnError>> {
  const { client: request = client, ...config } = options

  return withUnwrap(request({ method: 'POST', url: '/pn-ledger/entries/{Id}/reverse', security: [{ type: 'apiKey', name: 'sabai.auth', in: 'cookie', scopes: ['admin', 'member', 'pn'] }], contentType: { request: 'application/json' }, ...config, throwOnError: config.throwOnError ?? true }) as Promise<RequestResult<SaBaiAPIEndpointsPnLedgerReversePostEndpointResponses, ThrowOnError>>)
}

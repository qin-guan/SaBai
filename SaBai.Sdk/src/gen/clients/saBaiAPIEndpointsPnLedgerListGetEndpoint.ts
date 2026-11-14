import type { Options, Unwrappable, RequestResult } from '../.kubb/client'
import type { SaBaiAPIEndpointsPnLedgerListGetEndpointOptions, SaBaiAPIEndpointsPnLedgerListGetEndpointResponses } from '../types/SaBaiAPIEndpointsPnLedgerListGetEndpoint'
import { client, withUnwrap } from '../.kubb/client'

/**
 * @summary List per-PN balances and double-entry ledger entries
 * {@link /pn-ledger}
 */
export function saBaiAPIEndpointsPnLedgerListGetEndpoint<ThrowOnError extends boolean = true>(options: Options<SaBaiAPIEndpointsPnLedgerListGetEndpointOptions, ThrowOnError> = {}): Unwrappable<RequestResult<SaBaiAPIEndpointsPnLedgerListGetEndpointResponses, ThrowOnError>> {
  const { client: request = client, ...config } = options

  return withUnwrap(request({ method: 'GET', url: '/pn-ledger', security: [{ type: 'apiKey', name: 'sabai.auth', in: 'cookie', scopes: ['admin', 'member', 'pn'] }], ...config, throwOnError: config.throwOnError ?? true }) as Promise<RequestResult<SaBaiAPIEndpointsPnLedgerListGetEndpointResponses, ThrowOnError>>)
}

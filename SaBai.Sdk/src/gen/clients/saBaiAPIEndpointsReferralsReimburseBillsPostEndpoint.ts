import type { Options, Unwrappable, RequestResult } from '../.kubb/client'
import type {
  SaBaiAPIEndpointsReferralsReimburseBillsPostEndpointOptions,
  SaBaiAPIEndpointsReferralsReimburseBillsPostEndpointResponses,
} from '../types/SaBaiAPIEndpointsReferralsReimburseBillsPostEndpoint'
import { client, withUnwrap } from '../.kubb/client'

export function saBaiAPIEndpointsReferralsReimburseBillsPostEndpoint<ThrowOnError extends boolean = true>(
  options: Options<SaBaiAPIEndpointsReferralsReimburseBillsPostEndpointOptions, ThrowOnError>,
): Unwrappable<RequestResult<SaBaiAPIEndpointsReferralsReimburseBillsPostEndpointResponses, ThrowOnError>> {
  const { client: request = client, ...config } = options

  return withUnwrap(
    request({
      method: 'POST',
      url: '/referrals/bills/reimburse',
      security: [{ type: 'apiKey', name: 'sabai.auth', in: 'cookie', scopes: ['admin', 'member', 'pn'] }],
      contentType: { request: 'application/json' },
      ...config,
      throwOnError: config.throwOnError ?? true,
    }) as Promise<RequestResult<SaBaiAPIEndpointsReferralsReimburseBillsPostEndpointResponses, ThrowOnError>>,
  )
}

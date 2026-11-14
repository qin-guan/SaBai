import type { Options, Unwrappable, RequestResult } from '../.kubb/client'
import type {
  SaBaiAPIEndpointsReferralsRepayPnPostEndpointOptions,
  SaBaiAPIEndpointsReferralsRepayPnPostEndpointResponses,
} from '../types/SaBaiAPIEndpointsReferralsRepayPnPostEndpoint'
import { client, withUnwrap } from '../.kubb/client'

export function saBaiAPIEndpointsReferralsRepayPnPostEndpoint<ThrowOnError extends boolean = true>(
  options: Options<SaBaiAPIEndpointsReferralsRepayPnPostEndpointOptions, ThrowOnError>,
): Unwrappable<RequestResult<SaBaiAPIEndpointsReferralsRepayPnPostEndpointResponses, ThrowOnError>> {
  const { client: request = client, ...config } = options

  return withUnwrap(
    request({
      method: 'POST',
      url: '/referrals/bills/repay-pn',
      security: [{ type: 'apiKey', name: 'sabai.auth', in: 'cookie', scopes: ['admin', 'member', 'pn'] }],
      contentType: { request: 'application/json' },
      ...config,
      throwOnError: config.throwOnError ?? true,
    }) as Promise<RequestResult<SaBaiAPIEndpointsReferralsRepayPnPostEndpointResponses, ThrowOnError>>,
  )
}

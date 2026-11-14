import type { Options, Unwrappable, RequestResult } from '../.kubb/client'
import type {
  SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpointOptions,
  SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpointResponses,
} from '../types/SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpoint'
import { client, withUnwrap } from '../.kubb/client'

export function saBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpoint<ThrowOnError extends boolean = true>(
  options: Options<SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpointOptions, ThrowOnError>,
): Unwrappable<RequestResult<SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpointResponses, ThrowOnError>> {
  const { client: request = client, ...config } = options

  return withUnwrap(
    request({
      method: 'PATCH',
      url: '/referrals/{id}/reimbursement',
      security: [{ type: 'apiKey', name: 'sabai.auth', in: 'cookie', scopes: ['admin', 'member', 'pn'] }],
      contentType: { request: 'application/json' },
      ...config,
      throwOnError: config.throwOnError ?? true,
    }) as Promise<RequestResult<SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpointResponses, ThrowOnError>>,
  )
}

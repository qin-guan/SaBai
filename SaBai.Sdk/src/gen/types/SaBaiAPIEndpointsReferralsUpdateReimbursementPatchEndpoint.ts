import type { FastEndpointsProblemDetails } from './FastEndpointsProblemDetails'
import type { SaBaiAPIEndpointsReferralsGetResponse } from './SaBaiAPIEndpointsReferralsGetResponse'

export type SaBaiAPIEndpointsReferralsUpdateReimbursementPatchRequest = {
  isReimbursed: boolean;
  reimbursementNotes?: string | null;
  reimbursedUsd?: number | null;
  reimbursedRiels?: number | null;
};

export type SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpointPath = {
  id: string;
};

export type SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpointStatus200 = SaBaiAPIEndpointsReferralsGetResponse;
export type SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpointStatus400 = FastEndpointsProblemDetails;
export type SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpointStatus401 = unknown;
export type SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpointStatus403 = unknown;

export type SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpointBody = SaBaiAPIEndpointsReferralsUpdateReimbursementPatchRequest;

export type SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpointOptions = {
  body: SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpointBody;
  path: SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpointPath;
  query?: never;
  headers?: never;
};

export type SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpointResponses = {
  '200': SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpointStatus200;
  '400': SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpointStatus400;
  '401': SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpointStatus401;
  '403': SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpointStatus403;
};

export type SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpointResponse = SaBaiAPIEndpointsReferralsUpdateReimbursementPatchEndpointStatus200;

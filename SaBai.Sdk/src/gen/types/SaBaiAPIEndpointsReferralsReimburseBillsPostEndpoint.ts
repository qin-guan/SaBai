import type { FastEndpointsProblemDetails } from './FastEndpointsProblemDetails'

export type SaBaiAPIEndpointsReferralsReimburseBillsPostRequest = {
  referralIds: string[];
  isReimbursed: boolean;
  reimbursementNotes?: string | null;
};

export type SaBaiAPIEndpointsReferralsReimburseBillsPostResponse = {
  updatedCount: number;
  isReimbursed: boolean;
  reimbursedAt: string | null;
  reimbursedByUserId: string | null;
  reimbursementNotes: string | null;
};

export type SaBaiAPIEndpointsReferralsReimburseBillsPostEndpointStatus200 = SaBaiAPIEndpointsReferralsReimburseBillsPostResponse;
export type SaBaiAPIEndpointsReferralsReimburseBillsPostEndpointStatus400 = FastEndpointsProblemDetails;
export type SaBaiAPIEndpointsReferralsReimburseBillsPostEndpointStatus401 = unknown;
export type SaBaiAPIEndpointsReferralsReimburseBillsPostEndpointStatus403 = unknown;

export type SaBaiAPIEndpointsReferralsReimburseBillsPostEndpointBody = SaBaiAPIEndpointsReferralsReimburseBillsPostRequest;

export type SaBaiAPIEndpointsReferralsReimburseBillsPostEndpointOptions = {
  body: SaBaiAPIEndpointsReferralsReimburseBillsPostEndpointBody;
  path?: never;
  query?: never;
  headers?: never;
};

export type SaBaiAPIEndpointsReferralsReimburseBillsPostEndpointResponses = {
  '200': SaBaiAPIEndpointsReferralsReimburseBillsPostEndpointStatus200;
  '400': SaBaiAPIEndpointsReferralsReimburseBillsPostEndpointStatus400;
  '401': SaBaiAPIEndpointsReferralsReimburseBillsPostEndpointStatus401;
  '403': SaBaiAPIEndpointsReferralsReimburseBillsPostEndpointStatus403;
};

export type SaBaiAPIEndpointsReferralsReimburseBillsPostEndpointResponse = SaBaiAPIEndpointsReferralsReimburseBillsPostEndpointStatus200;

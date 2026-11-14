import type { FastEndpointsProblemDetails } from './FastEndpointsProblemDetails'

export type SaBaiAPIEndpointsReferralsRepayPnPostRequest = {
  memberWhoPaidId: string;
  amountUsd?: number | null;
  amountRiels?: number | null;
  reimbursementNotes?: string | null;
};

export type SaBaiAPIEndpointsReferralsRepayPnPostResponse = {
  updatedCount: number;
  appliedUsd: number;
  appliedRiels: number;
  reimbursedAt: string;
  reimbursedByUserId: string;
};

export type SaBaiAPIEndpointsReferralsRepayPnPostEndpointStatus200 = SaBaiAPIEndpointsReferralsRepayPnPostResponse;
export type SaBaiAPIEndpointsReferralsRepayPnPostEndpointStatus400 = FastEndpointsProblemDetails;
export type SaBaiAPIEndpointsReferralsRepayPnPostEndpointStatus401 = unknown;
export type SaBaiAPIEndpointsReferralsRepayPnPostEndpointStatus403 = unknown;

export type SaBaiAPIEndpointsReferralsRepayPnPostEndpointBody = SaBaiAPIEndpointsReferralsRepayPnPostRequest;

export type SaBaiAPIEndpointsReferralsRepayPnPostEndpointOptions = {
  body: SaBaiAPIEndpointsReferralsRepayPnPostEndpointBody;
  path?: never;
  query?: never;
  headers?: never;
};

export type SaBaiAPIEndpointsReferralsRepayPnPostEndpointResponses = {
  '200': SaBaiAPIEndpointsReferralsRepayPnPostEndpointStatus200;
  '400': SaBaiAPIEndpointsReferralsRepayPnPostEndpointStatus400;
  '401': SaBaiAPIEndpointsReferralsRepayPnPostEndpointStatus401;
  '403': SaBaiAPIEndpointsReferralsRepayPnPostEndpointStatus403;
};

export type SaBaiAPIEndpointsReferralsRepayPnPostEndpointResponse = SaBaiAPIEndpointsReferralsRepayPnPostEndpointStatus200;

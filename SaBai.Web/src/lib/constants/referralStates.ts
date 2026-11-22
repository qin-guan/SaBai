import {
  saBaiAPIDomainReferralState,
  type SaBaiAPIDomainReferralStateKey,
} from "@sabai/sdk/types";

// Declaration order of the API's ReferralState enum, i.e. the referral lifecycle.
export const REFERRAL_STATES = Object.values(saBaiAPIDomainReferralState);

export type ReferralState = SaBaiAPIDomainReferralStateKey;

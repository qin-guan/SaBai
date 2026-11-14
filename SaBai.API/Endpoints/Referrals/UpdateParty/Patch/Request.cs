namespace SaBai.API.Endpoints.Referrals.UpdateParty.Patch;

public sealed record Request(
    Guid Id,
    Guid? MemberOnRunId,
    bool PresentAtReferralParty,
    string? ReferralPartyResults,
    bool FollowUp,
    bool DoctorsUpdated
);

using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Referrals.UpdateState.Patch;

/// <summary>New referral state. An omitted outcome is not overwritten.</summary>
public sealed record Request
{
    public int Id { get; init; }

    public ReferralState? ReferralState { get; init; }

    public string? ReferralOutcome { get; init; }
}

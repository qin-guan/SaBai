namespace SaBai.API.Endpoints.Referrals.UpdateOutcome.Patch;

/// <summary>New referral outcome. The referral's state is left as it is. An empty outcome clears it.</summary>
public sealed record Request
{
    public int Id { get; init; }

    public string? ReferralOutcome { get; init; }
}

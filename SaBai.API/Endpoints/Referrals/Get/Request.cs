namespace SaBai.API.Endpoints.Referrals.Get;

/// <summary>Identifies a referral.</summary>
public sealed record Request
{
    public Guid Id { get; init; }
}

namespace SaBai.API.Endpoints.Referrals.Create.Post;

/// <summary>Referral to open against a consult. State starts as new.</summary>
public sealed record Request
{
    public int ConsultId { get; init; }

    public string ReferredFor { get; init; } = "";

    public string? ReferralNotes { get; init; }
}

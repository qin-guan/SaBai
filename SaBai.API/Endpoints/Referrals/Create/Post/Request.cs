namespace SaBai.API.Endpoints.Referrals.Create.Post;

/// <summary>Referral to open against a consult. State starts as new.</summary>
public sealed record Request
{
    /// <summary>Id for the new record, chosen by the client; generated when omitted. Resending a create with the same id returns the existing record.</summary>
    public Guid? Id { get; init; }

    public Guid ConsultId { get; init; }

    public string ReferredFor { get; init; } = "";

    public string? ReferralNotes { get; init; }
}

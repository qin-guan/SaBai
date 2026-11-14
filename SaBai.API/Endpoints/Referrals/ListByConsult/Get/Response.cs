using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Referrals.ListByConsult.Get;

/// <summary>Referral row without joined patient or doctor fields.</summary>
public sealed record Response(
    Guid Id,
    string ReferredFor,
    string? ReferralNotes,
    ReferralState ReferralState,
    string? ReferralOutcome,
    Guid ConsultId,
    DateTimeOffset CreatedAt
);

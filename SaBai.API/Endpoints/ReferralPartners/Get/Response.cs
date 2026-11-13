namespace SaBai.API.Endpoints.ReferralPartners.Get;

public sealed record Response(
    Guid Id,
    string Name,
    string? Remarks,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

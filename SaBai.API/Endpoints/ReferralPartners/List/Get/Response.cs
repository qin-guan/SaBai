namespace SaBai.API.Endpoints.ReferralPartners.List.Get;

public sealed record Response(
    Guid Id,
    string Name,
    string? Remarks,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

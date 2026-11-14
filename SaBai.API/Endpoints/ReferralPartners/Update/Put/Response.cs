namespace SaBai.API.Endpoints.ReferralPartners.Update.Put;

public sealed record Response(
    Guid Id,
    string Name,
    string? Remarks,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

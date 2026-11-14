using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.Consents.Get;

public sealed record Item(
    int Id,
    ConsentType ConsentType,
    bool Granted,
    DateTimeOffset RecordedAt,
    DateTimeOffset? RevokedAt,
    Guid RecordedByUserId,
    string? Notes
);

public sealed record Response(IReadOnlyList<Item> Consents);

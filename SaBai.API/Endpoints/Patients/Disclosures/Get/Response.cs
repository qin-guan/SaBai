namespace SaBai.API.Endpoints.Patients.Disclosures.Get;

public sealed record Item(
    int Id,
    string Recipient,
    string Purpose,
    string? Description,
    DateTimeOffset DisclosedAt,
    Guid DisclosedByUserId
);

public sealed record Response(IReadOnlyList<Item> Disclosures);

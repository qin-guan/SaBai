namespace SaBai.API.Endpoints.Patients.Disclosures.Get;

public sealed record Item(
    Guid Id,
    string Recipient,
    string Purpose,
    string? Description,
    DateTimeOffset DisclosedAt,
    Guid DisclosedByUserId
);

public sealed record Response(IReadOnlyList<Item> Disclosures);

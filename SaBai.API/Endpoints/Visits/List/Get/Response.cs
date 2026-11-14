namespace SaBai.API.Endpoints.Visits.List.Get;

/// <summary>Visits joined to patient and village, newest first.</summary>
public sealed record Response(IReadOnlyList<Item> Visits);

/// <summary>A visit joined to its patient and village.</summary>
public sealed record Item(
    Guid Id,
    DateTimeOffset Date,
    Guid PatientId,
    string PatientName,
    Guid VillageCodeId,
    string VillageCode,
    string VillageName,
    string VillageColorHex,
    string Status
);

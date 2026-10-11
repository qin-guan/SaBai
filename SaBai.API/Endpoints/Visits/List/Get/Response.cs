namespace SaBai.API.Endpoints.Visits.List.Get;

/// <summary>Visits joined to patient and village, newest first.</summary>
public sealed record Response(IReadOnlyList<Item> Visits);

/// <summary>A visit joined to its patient and village.</summary>
public sealed record Item(
    int Id,
    DateTimeOffset Date,
    int PatientId,
    string PatientName,
    int VillageCodeId,
    string VillageCode,
    string VillageName,
    string VillageColorHex,
    string Status);

namespace SaBai.API.Endpoints.Visits.Get;

/// <summary>A visit joined to its patient and village.</summary>
public sealed record Response(
    int Id,
    DateTimeOffset Date,
    int PatientId,
    string PatientName,
    int VillageCodeId,
    string VillageCode,
    string VillageName,
    string VillageColorHex,
    string Status
);

namespace SaBai.API.Endpoints.Visits.Update.Patch;

/// <summary>A visit after a partial update.</summary>
public sealed record Response(
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

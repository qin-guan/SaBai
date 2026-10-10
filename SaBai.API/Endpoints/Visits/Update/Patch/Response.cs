namespace SaBai.API.Endpoints.Visits.Update.Patch;

/// <summary>A visit after a partial update.</summary>
public sealed record Response(
    int Id,
    DateTimeOffset Date,
    int PatientId,
    string PatientName,
    int VillageCodeId,
    string VillageCode,
    string VillageName,
    string VillageColorHex,
    string Status);

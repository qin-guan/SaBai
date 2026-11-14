namespace SaBai.API.Endpoints.Visits.Create.Post;

/// <summary>A newly created visit joined to its patient and village.</summary>
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

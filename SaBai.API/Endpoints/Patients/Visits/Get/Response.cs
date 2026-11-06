namespace SaBai.API.Endpoints.Patients.Visits.Get;

/// <summary>Visits for one patient, newest first.</summary>
public sealed record Response(IReadOnlyList<Item> Visits);

/// <summary>A visit joined to its patient and village, with where it is in the clinic flow.</summary>
public sealed record Item(
    int Id,
    DateTimeOffset Date,
    int PatientId,
    string PatientName,
    int VillageCodeId,
    string VillageCode,
    string VillageName,
    string VillageColorHex,
    string Status,
    VisitJourney Journey
);

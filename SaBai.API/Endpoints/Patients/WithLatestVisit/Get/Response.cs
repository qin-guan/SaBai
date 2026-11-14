using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.WithLatestVisit.Get;

/// <summary>Patients with a visit first, then by latest visit date descending.</summary>
public sealed record Response(IReadOnlyList<Item> Patients);

/// <summary>A patient row including village color, latest visit and where that visit is in the clinic flow.</summary>
public sealed record Item(
    Guid Id,
    string Name,
    string? IdentificationNumber,
    string? ContactNo,
    Gender Gender,
    DateTimeOffset DateOfBirth,
    string DrugAllergy,
    bool HasPoorCard,
    bool HasBS2Card,
    bool HasSabaiCard,
    bool ToGetReport,
    Guid? VillageCodeId,
    string? VillageCode,
    string? VillageColorHex,
    string DisplayId,
    string FilterString,
    DateTimeOffset? LastVisitDate,
    Guid? LastVisitId,
    bool HasPhoto,
    bool HasFace,
    VisitJourney? LastVisitJourney,
    IReadOnlyList<string> Tags
);

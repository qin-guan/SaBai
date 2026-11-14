using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.List.Get;

/// <summary>Patients ordered by id.</summary>
public sealed record Response(IReadOnlyList<Item> Patients);

/// <summary>A patient row including display id and latest visit.</summary>
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
    string DisplayId,
    string FilterString,
    DateTimeOffset? LastVisitDate,
    Guid? LastVisitId,
    bool HasPhoto,
    bool HasFace
);

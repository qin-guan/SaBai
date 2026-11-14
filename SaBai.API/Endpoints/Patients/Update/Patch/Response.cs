using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.Update.Patch;

/// <summary>A patient after a partial update.</summary>
public sealed record Response(
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

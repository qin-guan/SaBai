using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.Update.Patch;

/// <summary>A patient after a partial update.</summary>
public sealed record Response(
    int Id,
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
    int? VillageCodeId,
    string? VillageCode,
    string DisplayId,
    string FilterString,
    DateTimeOffset? LastVisitDate,
    int? LastVisitId,
    bool HasPhoto,
    bool HasFace);

using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.Create.Post;

/// <summary>Newly registered patient, the visit opened for them, and vitals when captured.</summary>
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
    DateTimeOffset LastVisitDate,
    Guid LastVisitId,
    Guid VisitId,
    Guid? VitalsId,
    bool HasPhoto,
    bool HasFace
);

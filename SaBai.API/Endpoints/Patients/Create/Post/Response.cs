using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.Create.Post;

/// <summary>Newly registered patient, the visit opened for them, and vitals when captured.</summary>
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
    DateTimeOffset LastVisitDate,
    int LastVisitId,
    int VisitId,
    int? VitalsId,
    bool HasPhoto,
    bool HasFace
);

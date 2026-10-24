using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.Update.Patch;

/// <summary>Partial patient update. Image fields are not accepted.</summary>
public sealed record Request
{
    public int Id { get; init; }

    public string? Name { get; init; }

    public string? IdentificationNumber { get; init; }

    public string? ContactNo { get; init; }

    public Gender? Gender { get; init; }

    public DateTimeOffset? DateOfBirth { get; init; }

    public string? DrugAllergy { get; init; }

    public bool? HasPoorCard { get; init; }

    public bool? HasBS2Card { get; init; }

    public bool? HasSabaiCard { get; init; }

    public bool? ToGetReport { get; init; }

    public int? VillageCodeId { get; init; }
}

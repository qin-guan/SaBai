using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.Create.Post;

/// <summary>Registration payload. A visit is opened for the patient in the same transaction.</summary>
public sealed record Request
{
    public required string Name { get; init; }

    public string? IdentificationNumber { get; init; }

    public string? ContactNo { get; init; }

    public Gender? Gender { get; init; }

    public DateTimeOffset? DateOfBirth { get; init; }

    public required string DrugAllergy { get; init; }

    public bool HasPoorCard { get; init; }

    public bool HasBS2Card { get; init; }

    public bool HasSabaiCard { get; init; }

    public bool ToGetReport { get; init; }

    public int VillageCodeId { get; init; }

    public VitalsRequest? Vitals { get; init; }
}

/// <summary>Optional vitals captured during registration.</summary>
public sealed record VitalsRequest
{
    public decimal? Temperature { get; init; }

    public decimal? Height { get; init; }

    public decimal? Weight { get; init; }

    public int? Systolic { get; init; }

    public int? Diastolic { get; init; }

    public int? HeartRate { get; init; }

    public decimal? HemocueCount { get; init; }

    public bool? DiabetesMellitus { get; init; }

    public string? UrineTest { get; init; }

    public decimal? BloodGlucoseNonFasting { get; init; }

    public decimal? BloodGlucoseFasting { get; init; }

    public decimal? Hba1c { get; init; }

    public string? Others { get; init; }
}

using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.Create.Post;

/// <summary>Registration payload. A visit is opened for the patient in the same transaction.</summary>
public sealed record Request
{
    /// <summary>Id for the new patient, chosen by the client; generated when omitted. Resending a registration with the same id returns the existing patient.</summary>
    public Guid? Id { get; init; }

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

    public Guid VillageCodeId { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }

    public VitalsRequest? Vitals { get; init; }

    /// <summary>Id for the visit opened with the registration, chosen by the client; generated when omitted.</summary>
    public Guid? VisitId { get; init; }

    /// <summary>Id for the vitals recorded with the registration, chosen by the client; generated when omitted. Ignored without <see cref="Vitals"/>.</summary>
    public Guid? VitalsId { get; init; }
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

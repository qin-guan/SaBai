namespace SaBai.API.Endpoints.Vitals.UpdateByVisit.Patch;

/// <summary>Partial vitals update for a visit. Omitted measurements are left unchanged, and the visit cannot be changed.</summary>
public sealed record Request
{
    public int VisitId { get; init; }

    public decimal? Height { get; init; }

    public decimal? Weight { get; init; }

    public decimal? Temperature { get; init; }

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

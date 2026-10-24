namespace SaBai.API.Endpoints.Vitals.Update.Patch;

/// <summary>Vitals recorded for a visit.</summary>
public sealed record Response(
    int Id,
    int VisitId,
    decimal? Height,
    decimal? Weight,
    decimal? Temperature,
    int? Systolic,
    int? Diastolic,
    int? HeartRate,
    decimal? HemocueCount,
    bool? DiabetesMellitus,
    string? UrineTest,
    decimal? BloodGlucoseNonFasting,
    decimal? BloodGlucoseFasting,
    decimal? Hba1c,
    string? Others
)
{
    public static Response From(global::SaBai.API.Domain.Vitals vitals) =>
        new(
            vitals.Id,
            vitals.VisitId,
            vitals.Height,
            vitals.Weight,
            vitals.Temperature,
            vitals.Systolic,
            vitals.Diastolic,
            vitals.HeartRate,
            vitals.HemocueCount,
            vitals.DiabetesMellitus,
            vitals.UrineTest,
            vitals.BloodGlucoseNonFasting,
            vitals.BloodGlucoseFasting,
            vitals.Hba1c,
            vitals.Others
        );
}

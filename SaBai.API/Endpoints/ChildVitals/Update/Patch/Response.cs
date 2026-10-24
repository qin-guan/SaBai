namespace SaBai.API.Endpoints.ChildVitals.Update.Patch;

/// <summary>Child vitals recorded against a vitals record.</summary>
public sealed record Response(
    int Id,
    int VitalsId,
    string? GrossMotor,
    string? RedReflex,
    string? Scoliosis,
    bool? Pallor,
    string? OralCavity,
    string? Heart,
    string? Abdomen,
    string? Lungs,
    string? HernialOrifices
)
{
    public static Response From(global::SaBai.API.Domain.ChildVitals childVitals) =>
        new(
            childVitals.Id,
            childVitals.VitalsId,
            childVitals.GrossMotor,
            childVitals.RedReflex,
            childVitals.Scoliosis,
            childVitals.Pallor,
            childVitals.OralCavity,
            childVitals.Heart,
            childVitals.Abdomen,
            childVitals.Lungs,
            childVitals.HernialOrifices
        );
}

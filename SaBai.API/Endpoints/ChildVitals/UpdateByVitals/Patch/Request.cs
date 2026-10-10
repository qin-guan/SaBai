namespace SaBai.API.Endpoints.ChildVitals.UpdateByVitals.Patch;

/// <summary>Partial child vitals update for a vitals record. Omitted fields are left unchanged, and the vitals record cannot be changed.</summary>
public sealed record Request
{
    public int VitalsId { get; init; }

    public string? GrossMotor { get; init; }

    public string? RedReflex { get; init; }

    public string? Scoliosis { get; init; }

    public bool? Pallor { get; init; }

    public string? OralCavity { get; init; }

    public string? Heart { get; init; }

    public string? Abdomen { get; init; }

    public string? Lungs { get; init; }

    public string? HernialOrifices { get; init; }
}

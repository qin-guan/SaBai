namespace SaBai.API.Endpoints.ChildVitals.Create.Post;

/// <summary>Child vitals to record against a vitals record.</summary>
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

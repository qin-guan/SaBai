namespace SaBai.API.Endpoints.Diagnoses.Get;

/// <summary>Identifies a diagnosis.</summary>
public sealed record Request
{
    public Guid Id { get; init; }
}

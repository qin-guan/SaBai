namespace SaBai.API.Endpoints.Diagnoses.Delete.Delete;

/// <summary>Identifies a diagnosis to delete.</summary>
public sealed record Request
{
    public int Id { get; init; }
}

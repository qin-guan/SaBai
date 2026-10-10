namespace SaBai.API.Endpoints.ChildVitals.Delete;

/// <summary>Identifies child vitals to delete.</summary>
public sealed record Request
{
    public int Id { get; init; }
}

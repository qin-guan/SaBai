namespace SaBai.API.Endpoints.ChildVitals.Get;

/// <summary>Identifies a child vitals record.</summary>
public sealed record Request
{
    public int Id { get; init; }
}

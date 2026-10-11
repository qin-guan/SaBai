namespace SaBai.API.Endpoints.Vitals.Get;

/// <summary>Identifies a vitals record.</summary>
public sealed record Request
{
    public int Id { get; init; }
}

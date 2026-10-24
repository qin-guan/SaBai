namespace SaBai.API.Endpoints.VillageCodes.Get;

/// <summary>Identifies a village code.</summary>
public sealed record Request
{
    public int Id { get; init; }
}

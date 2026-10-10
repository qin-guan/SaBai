namespace SaBai.API.Endpoints.VillageCodes.Delete.Delete;

/// <summary>Identifies a village code to delete.</summary>
public sealed record Request
{
    public int Id { get; init; }
}

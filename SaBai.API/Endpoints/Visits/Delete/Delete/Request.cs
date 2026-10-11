namespace SaBai.API.Endpoints.Visits.Delete.Delete;

/// <summary>Identifies a visit to delete.</summary>
public sealed record Request
{
    public int Id { get; init; }
}

namespace SaBai.API.Endpoints.Vitals.Delete;

/// <summary>Identifies vitals to delete.</summary>
public sealed record Request
{
    public int Id { get; init; }
}

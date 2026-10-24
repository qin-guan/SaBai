namespace SaBai.API.Endpoints.Broadcasts.Delete.Delete;

/// <summary>Identifies the broadcast to delete.</summary>
public sealed record Request
{
    public int Id { get; init; }
}

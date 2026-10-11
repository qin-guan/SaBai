namespace SaBai.API.Endpoints.Broadcasts.Publish.Post;

/// <summary>Identifies the broadcast to mark as published.</summary>
public sealed record Request
{
    public int Id { get; init; }
}

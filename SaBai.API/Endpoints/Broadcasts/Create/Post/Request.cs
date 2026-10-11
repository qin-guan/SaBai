using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Broadcasts.Create.Post;

/// <summary>New unpublished broadcast. Severity defaults to info.</summary>
public sealed record Request
{
    public required string Title { get; init; }

    public required string Body { get; init; }

    public BroadcastSeverity Severity { get; init; } = BroadcastSeverity.Info;
}

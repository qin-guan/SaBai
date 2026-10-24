using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Broadcasts.Publish.Post;

/// <summary>Broadcast after a publish attempt. PublishedAt is set only once.</summary>
public sealed record Response(
    int Id,
    string Title,
    string Body,
    BroadcastSeverity Severity,
    string? CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    int Attempts);

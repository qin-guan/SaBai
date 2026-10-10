using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Broadcasts.Create.Post;

/// <summary>Created broadcast that has not yet been published.</summary>
public sealed record Response(
    int Id,
    string Title,
    string Body,
    BroadcastSeverity Severity,
    string? CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    int Attempts);

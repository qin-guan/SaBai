using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Broadcasts.List.Get;

/// <summary>Clinic broadcast, newest first.</summary>
public sealed record Response(
    int Id,
    string Title,
    string Body,
    BroadcastSeverity Severity,
    string? CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    int Attempts
);

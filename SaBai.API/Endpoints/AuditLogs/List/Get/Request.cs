using SaBai.API.Domain;

namespace SaBai.API.Endpoints.AuditLogs.List.Get;

/// <summary>Audit log filters. All are optional and combine with AND.</summary>
public sealed record Request
{
    /// <summary>Entity type, e.g. <c>Patient</c> or <c>MedicationStock</c>.</summary>
    public string? EntityType { get; init; }

    /// <summary>Primary key of one row; use with <c>entityType</c>.</summary>
    public string? EntityId { get; init; }

    /// <summary>Only events caused by this staff user.</summary>
    public Guid? UserId { get; init; }

    public AuditAction? Action { get; init; }

    /// <summary>Only events at or after this time.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Only events before this time.</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>Paging cursor: return entries older than this id (the previous page's <c>nextBefore</c>).</summary>
    public Guid? Before { get; init; }

    /// <summary>Page size, 1–200. Defaults to 50.</summary>
    public int? Take { get; init; }
}

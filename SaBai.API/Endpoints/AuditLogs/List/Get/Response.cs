using SaBai.API.Domain;

namespace SaBai.API.Endpoints.AuditLogs.List.Get;

/// <summary>A page of audit entries, newest first.</summary>
/// <param name="NextBefore">Pass as <c>before</c> to get the next (older) page; null on the last page.</param>
public sealed record Response(IReadOnlyList<AuditLogEntryResponse> Entries, int? NextBefore);

public sealed record AuditLogEntryResponse(
    int Id,
    DateTimeOffset OccurredAt,
    AuditAction Action,
    Guid? UserId,
    string? UserName,
    string? EntityType,
    string? EntityId,
    IReadOnlyList<AuditFieldChangeResponse> Changes,
    string? Details,
    string? IpAddress,
    string? Request
);

public sealed record AuditFieldChangeResponse(string Field, string? Old, string? New);

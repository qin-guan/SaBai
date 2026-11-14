using SaBai.API.Domain;

namespace SaBai.API.Data;

/// <summary>
/// Records audit events that are not row changes, such as sign-ins. Row changes are
/// logged automatically by <see cref="AuditInterceptor"/>.
/// </summary>
public sealed class AuditTrail(AppDbContext db, IHttpContextAccessor http, TimeProvider clock)
{
    /// <summary>Longest attempted username kept for a failed sign-in.</summary>
    private const int MaxUserNameLength = 150;

    /// <summary>
    /// Saves one event. <paramref name="user"/> is the account it concerns; when null the
    /// signed-in user of the request is used, or <paramref name="userName"/> alone (an
    /// attempted name that matched no account).
    /// </summary>
    public async Task RecordAsync(
        AuditAction action,
        StaffUser? user = null,
        string? userName = null,
        string? details = null,
        string? entityType = null,
        string? entityId = null,
        CancellationToken ct = default
    )
    {
        var actor = AuditActor.From(http.HttpContext);
        var name = user?.UserName ?? userName ?? actor.UserName;
        db.AuditLogs.Add(
            new AuditLogEntry
            {
                OccurredAt = clock.GetUtcNow(),
                Action = action,
                UserId = user?.Id ?? actor.UserId,
                UserName = name is { Length: > MaxUserNameLength } ? name[..MaxUserNameLength] : name,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                IpAddress = actor.IpAddress,
                Request = actor.Request,
            }
        );
        await db.SaveChangesAsync(ct);
    }
}

namespace SaBai.API.Domain;

/// <summary>What an <see cref="AuditLogEntry"/> records.</summary>
public enum AuditAction
{
    /// <summary>A row was inserted.</summary>
    Created,

    /// <summary>One or more columns of a row changed.</summary>
    Updated,

    /// <summary>A row was removed.</summary>
    Deleted,

    /// <summary>A staff user finished signing in and got a session.</summary>
    SignedIn,

    /// <summary>A sign-in step was rejected (wrong password, code, or locked account).</summary>
    SignInFailed,

    /// <summary>A staff user ended their session.</summary>
    SignedOut,
}

/// <summary>
/// One audited event: a data change written through <c>AppDbContext</c>, or a
/// sign-in event. Rows are append-only and keep the actor's name as it was, so they
/// stay readable after the staff account is renamed or deleted (there is no foreign
/// key to <c>StaffUsers</c> on purpose).
/// </summary>
public sealed class AuditLogEntry
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAt { get; set; }

    public AuditAction Action { get; set; }

    /// <summary>Staff user who caused the event; null for system work (startup seeding) or anonymous requests.</summary>
    public Guid? UserId { get; set; }

    /// <summary>The actor's username at the time, or the attempted name for a failed sign-in.</summary>
    public string? UserName { get; set; }

    /// <summary>Entity type that changed (e.g. <c>Patient</c>); null for sign-in events.</summary>
    public string? EntityType { get; set; }

    /// <summary>Primary key of the changed row; composite keys are comma-separated.</summary>
    public string? EntityId { get; set; }

    /// <summary>Changed columns with old and new values. Secrets are recorded as <c>[redacted]</c>.</summary>
    public List<AuditFieldChange> Changes { get; set; } = [];

    /// <summary>Free-text context, such as why a sign-in failed.</summary>
    public string? Details { get; set; }

    public string? IpAddress { get; set; }

    /// <summary>HTTP method and path of the request that caused the event.</summary>
    public string? Request { get; set; }
}

/// <summary>One column's value before and after. <c>Old</c> is null on create, <c>New</c> on delete.</summary>
public sealed class AuditFieldChange
{
    public required string Field { get; set; }

    public string? Old { get; set; }

    public string? New { get; set; }
}

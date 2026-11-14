namespace SaBai.API.Domain;

/// <summary>
/// Implemented by every table replicated to PowerSync clients so <see cref="Data.AppDbContext"/>
/// can maintain <see cref="Version"/> and <see cref="UpdatedAt"/> automatically on every save.
/// </summary>
public interface ISyncEntity
{
    Guid Id { get; set; }
    int Version { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Records a same-field conflict resolved during offline sync (e.g. two devices edited the
/// same field while offline). Last-write-wins applies the incoming value and preserves the
/// overwritten value here for staff review.
/// </summary>
public sealed class SyncConflict
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Table { get; set; }
    public Guid RecordId { get; set; }
    public required string Field { get; set; }
    public string? PreviousValue { get; set; }
    public string? IncomingValue { get; set; }
    public string? WinningValue { get; set; }
    public Guid? IncomingUserId { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
}

/// <summary>
/// Records an offline mutation that the server could not apply (validation failure,
/// missing permission, or consult past its 24h edit window) so it is never lost silently.
/// </summary>
public sealed class SyncRejection
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Table { get; set; }
    public Guid RecordId { get; set; }
    public required string Operation { get; set; }
    public required string PayloadJson { get; set; }
    public required string Reason { get; set; }
    public Guid? UserId { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
}

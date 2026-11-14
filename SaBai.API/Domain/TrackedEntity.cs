namespace SaBai.API.Domain;

/// <summary>
/// A clinic record whose last change is stamped by <see cref="Data.AppDbContext"/> on every
/// save: when it happened and which staff user made it. A request that carries
/// <c>If-Unmodified-Since</c> is refused when it would change a record that another user
/// changed after that time (see <see cref="PreconditionFailedException"/>).
/// </summary>
public interface ITrackedEntity
{
    DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Staff user who last created or changed the record; null when no one was signed in (seeding, jobs).</summary>
    Guid? UpdatedByUserId { get; set; }
}

/// <summary>
/// A request's <c>If-Unmodified-Since</c> precondition failed: it would change or delete a
/// record that a different user changed after that time. Answered with 412.
/// </summary>
public sealed class PreconditionFailedException(string recordType, string recordId, DateTimeOffset changedAt)
    : Exception($"{recordType} {recordId} was changed by another user at {changedAt:O}.")
{
    public string RecordType { get; } = recordType;

    public string RecordId { get; } = recordId;

    public DateTimeOffset ChangedAt { get; } = changedAt;
}

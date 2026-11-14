namespace SaBai.API.Domain;

/// <summary>Freeform label attached to staff users or patients for quick identification.</summary>
public sealed class Tag
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    /// <summary>Uppercased invariant name used for case-insensitive uniqueness and lookup.</summary>
    public required string NormalizedName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<StaffUserTag> StaffUsers { get; set; } = [];

    public ICollection<PatientTag> Patients { get; set; } = [];
}

public sealed class StaffUserTag
{
    public Guid StaffUserId { get; set; }

    public StaffUser StaffUser { get; set; } = null!;

    public Guid TagId { get; set; }

    public Tag Tag { get; set; } = null!;
}

/// <summary>
/// Patient ↔ tag link. Carries its own UUIDv7 key (unique on PatientId + TagId) so it can be
/// replicated to offline clients by PowerSync, which requires a single <c>id</c> column.
/// </summary>
public sealed class PatientTag
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public Guid TagId { get; set; }

    public Tag Tag { get; set; } = null!;
}

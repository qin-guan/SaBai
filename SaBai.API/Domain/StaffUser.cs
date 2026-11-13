using Microsoft.AspNetCore.Identity;

namespace SaBai.API.Domain;

/// <summary>
/// Staff account. Credentials, lockout, two-factor and roles are managed by
/// ASP.NET Core Identity (<c>UserManager&lt;StaffUser&gt;</c>); this class only adds
/// the profile fields the clinic needs.
/// </summary>
public sealed class StaffUser : IdentityUser<Guid>
{
    public required string Nickname { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<StaffUserTag> UserTags { get; set; } = [];

    /// <summary>
    /// True while Identity considers the account locked out, whether an admin locked
    /// it or too many failed sign-ins locked it temporarily.
    /// </summary>
    public bool IsLockedOut(DateTimeOffset now) =>
        LockoutEnabled && LockoutEnd is { } end && end > now;
}

public sealed class Tag
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<StaffUserTag> UserTags { get; set; } = [];
}

public sealed class StaffUserTag
{
    public Guid StaffUserId { get; set; }

    public StaffUser StaffUser { get; set; } = null!;

    public int TagId { get; set; }

    public Tag Tag { get; set; } = null!;
}

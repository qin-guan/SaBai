namespace SaBai.API.Domain;

/// <summary>Local staff profile. Passwords live in WorkOS, not in this row.</summary>
public sealed class StaffUser
{
    public Guid Id { get; set; }

    public required string WorkOsUserId { get; set; }

    public required string Username { get; set; }

    public required string Email { get; set; }

    public required string Nickname { get; set; }

    public StaffRole Role { get; set; } = StaffRole.Member;

    public bool IsLocked { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.Users.Update.Patch;

/// <summary>Partial staff profile update. Password, when set, is sent to WorkOS.</summary>
public sealed record Request
{
    public Guid Id { get; init; }

    public string? Username { get; init; }

    public string? Email { get; init; }

    public string? Nickname { get; init; }

    public string? Password { get; init; }

    public StaffRole? Role { get; init; }
}

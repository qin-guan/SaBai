using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.Users.Create.Post;

/// <summary>Admin payload for provisioning a staff account.</summary>
public sealed record Request
{
    public required string Username { get; init; }

    public required string Email { get; init; }

    public required string Password { get; init; }

    public string? Nickname { get; init; }

    public StaffRole Role { get; init; } = StaffRole.Member;
}

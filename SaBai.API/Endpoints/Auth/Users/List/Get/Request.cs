using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.Users.List.Get;

/// <summary>Optional filters for the staff directory.</summary>
public sealed record Request
{
    public StaffRole? Role { get; init; }

    public string? Email { get; init; }

    public bool? IsLocked { get; init; }
}

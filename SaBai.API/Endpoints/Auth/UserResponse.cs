using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth;

/// <summary>Staff profile returned by authentication and user endpoints.</summary>
public sealed record UserResponse(
    Guid Id,
    string WorkOsUserId,
    string Username,
    string Email,
    string Nickname,
    StaffRole Role,
    bool IsLocked,
    DateTimeOffset CreatedAt)
{
    public static UserResponse From(StaffUser user) =>
        new(user.Id, user.WorkOsUserId, user.Username, user.Email, user.Nickname, user.Role, user.IsLocked, user.CreatedAt);
}

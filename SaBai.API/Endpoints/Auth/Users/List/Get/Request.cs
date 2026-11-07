namespace SaBai.API.Endpoints.Auth.Users.List.Get;

/// <summary>Optional filters for the staff directory.</summary>
public sealed record Request
{
    /// <summary>Only users in this role (for example <c>admin</c>).</summary>
    public string? Role { get; init; }

    public string? Email { get; init; }

    public bool? IsLocked { get; init; }
}

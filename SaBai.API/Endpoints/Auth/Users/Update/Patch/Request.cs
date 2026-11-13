namespace SaBai.API.Endpoints.Auth.Users.Update.Patch;

/// <summary>Partial staff profile update. Omitted fields are left as they are.</summary>
public sealed record Request
{
    public Guid Id { get; init; }

    public string? UserName { get; init; }

    /// <summary>An empty string removes the email.</summary>
    public string? Email { get; init; }

    public string? Nickname { get; init; }

    /// <summary>The full set of roles the user should have.</summary>
    public IReadOnlyList<string>? Roles { get; init; }

    /// <summary>The full set of tags the user should have.</summary>
    public IReadOnlyList<string>? Tags { get; init; }
}

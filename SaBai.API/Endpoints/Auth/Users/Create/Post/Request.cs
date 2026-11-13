namespace SaBai.API.Endpoints.Auth.Users.Create.Post;

/// <summary>Admin payload for provisioning a staff account.</summary>
public sealed record Request
{
    public required string UserName { get; init; }

    /// <summary>Optional. Can also be used to sign in.</summary>
    public string? Email { get; init; }

    public required string Password { get; init; }

    /// <summary>Display name; defaults to the username.</summary>
    public string? Nickname { get; init; }

    /// <summary>Role names from <c>GET /auth/roles</c>. Defaults to <c>member</c>.</summary>
    public IReadOnlyList<string>? Roles { get; init; }

    /// <summary>Optional staff tags (e.g. <c>PN</c>, <c>PL</c>).</summary>
    public IReadOnlyList<string>? Tags { get; init; }
}

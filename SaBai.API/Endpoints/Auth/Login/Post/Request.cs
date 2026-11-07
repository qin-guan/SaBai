namespace SaBai.API.Endpoints.Auth.Login.Post;

/// <summary>Username (or email) and password.</summary>
public sealed record Request
{
    /// <summary>Username, or the account's email address.</summary>
    public required string UserName { get; init; }

    public required string Password { get; init; }

    /// <summary>Keep the session cookie after the browser closes.</summary>
    public bool RememberMe { get; init; }
}

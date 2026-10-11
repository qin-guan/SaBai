namespace SaBai.API.Endpoints.Auth.Login.Post;

/// <summary>Email and password credentials for WorkOS authentication.</summary>
public sealed record Request
{
    public required string Email { get; init; }

    public required string Password { get; init; }
}

namespace SaBai.API.Endpoints.Auth.Users.ResetPassword.Post;

/// <summary>A new password chosen by an admin.</summary>
public sealed record Request
{
    public Guid Id { get; init; }

    public required string Password { get; init; }
}

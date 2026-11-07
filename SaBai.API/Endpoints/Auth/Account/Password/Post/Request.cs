namespace SaBai.API.Endpoints.Auth.Account.Password.Post;

/// <summary>Change the signed-in user's own password.</summary>
public sealed record Request
{
    public required string CurrentPassword { get; init; }

    public required string NewPassword { get; init; }
}

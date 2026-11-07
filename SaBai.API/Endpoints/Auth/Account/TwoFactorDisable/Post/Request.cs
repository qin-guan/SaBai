namespace SaBai.API.Endpoints.Auth.Account.TwoFactorDisable.Post;

/// <summary>Your password, to confirm turning two-factor off.</summary>
public sealed record Request
{
    public required string Password { get; init; }
}

namespace SaBai.API.Endpoints.Auth.Account.TwoFactorEnable.Post;

/// <summary>The current code from the authenticator app, proving it was set up.</summary>
public sealed record Request
{
    public required string Code { get; init; }
}

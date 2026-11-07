namespace SaBai.API.Endpoints.Auth.LoginRecoveryCode.Post;

/// <summary>Second sign-in step when the authenticator is not available.</summary>
public sealed record Request
{
    public required string RecoveryCode { get; init; }
}

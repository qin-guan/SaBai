namespace SaBai.API.Endpoints.Auth.LoginTwoFactor.Post;

/// <summary>Second sign-in step: the 6-digit code from the authenticator app.</summary>
public sealed record Request
{
    public required string Code { get; init; }

    /// <summary>Same as on the password step: keep the session after the browser closes.</summary>
    public bool RememberMe { get; init; }

    /// <summary>Skip the code on this browser for future sign-ins.</summary>
    public bool RememberMachine { get; init; }
}

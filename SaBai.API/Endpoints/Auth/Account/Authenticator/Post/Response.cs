namespace SaBai.API.Endpoints.Auth.Account.Authenticator.Post;

/// <summary>A new authenticator secret, not active until confirmed with a code.</summary>
/// <param name="SharedKey">The secret in groups of four, for typing into an app.</param>
/// <param name="AuthenticatorUri">An <c>otpauth://</c> URI to show as a QR code.</param>
public sealed record Response(string SharedKey, string AuthenticatorUri);

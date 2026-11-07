namespace SaBai.API.Endpoints.Auth.Account.TwoFactor.Get;

/// <summary>Two-factor state of the signed-in user.</summary>
/// <param name="IsMachineRemembered">This browser skips the code at sign-in.</param>
public sealed record Response(bool IsEnabled, int RecoveryCodesLeft, bool IsMachineRemembered);

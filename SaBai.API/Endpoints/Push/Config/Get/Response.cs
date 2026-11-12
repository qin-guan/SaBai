namespace SaBai.API.Endpoints.Push.Config.Get;

/// <summary>Whether this server can send notifications.</summary>
/// <param name="PublicKey">
/// VAPID public key (base64url) to pass as <c>applicationServerKey</c> when
/// subscribing. Null when notifications are off.
/// </param>
/// <param name="DeviceCount">How many of the signed-in user's devices receive notifications.</param>
public sealed record Response(bool IsEnabled, string? PublicKey, int DeviceCount);

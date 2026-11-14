using Microsoft.AspNetCore.WebUtilities;

namespace SaBai.API.Push;

/// <summary>
/// Web Push configuration ("Push" section). Notifications stay off until the VAPID
/// key pair and subject are set; generate a pair with
/// <c>npx web-push generate-vapid-keys</c>.
/// </summary>
public sealed class PushSettings
{
    public const string SectionName = "Push";

    /// <summary>
    /// Contact for the push services: a <c>mailto:</c> address or an <c>https://</c>
    /// URL. Apple rejects messages without a real one.
    /// </summary>
    public string Subject { get; set; } = "";

    /// <summary>VAPID public key (base64url, 65 bytes). Given to browsers when they subscribe.</summary>
    public string PublicKey { get; set; } = "";

    /// <summary>VAPID private key (base64url, 32 bytes). A secret; it signs every message.</summary>
    public string PrivateKey { get; set; } = "";

    /// <summary>
    /// Push services a device may subscribe through, as host names or parent domains.
    /// The API posts to the endpoint a browser hands it, so anything else is refused.
    /// Defaults to Chrome/Edge/Android (Google), Firefox, Windows and Apple. A browser
    /// whose push service is missing here gets "push service is not supported" when
    /// turning notifications on; add its host to this list.
    /// </summary>
    public string[]? AllowedHosts { get; set; }

    private static readonly string[] DefaultAllowedHosts =
    [
        "fcm.googleapis.com",
        "jmt17.google.com",
        "android.googleapis.com",
        "push.services.mozilla.com",
        "notify.windows.com",
        "push.apple.com",
    ];

    public bool IsEnabled => Problem is null;

    /// <summary>Why notifications are off, or null when the configuration is usable.</summary>
    public string? Problem
    {
        get
        {
            if (PublicKey.Length == 0 && PrivateKey.Length == 0 && Subject.Length == 0)
            {
                return "Push:PublicKey, Push:PrivateKey and Push:Subject are not set.";
            }

            if (!HasLength(PublicKey, 65))
            {
                return "Push:PublicKey is not a VAPID public key (base64url, 65 bytes).";
            }

            if (!HasLength(PrivateKey, 32))
            {
                return "Push:PrivateKey is not a VAPID private key (base64url, 32 bytes).";
            }

            if (
                !Subject.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                && !Subject.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            )
            {
                return "Push:Subject must be a mailto: address or an https:// URL.";
            }

            return null;
        }
    }

    /// <summary>True when the endpoint is an HTTPS URL at an allowed push service.</summary>
    public bool IsAllowedEndpoint(string endpoint)
    {
        if (
            !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || uri.UserInfo.Length > 0
        )
        {
            return false;
        }

        var allowed = AllowedHosts is { Length: > 0 } ? AllowedHosts : DefaultAllowedHosts;
        return allowed.Any(host =>
            uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>True when the text is base64url for exactly this many bytes.</summary>
    public static bool HasLength(string base64Url, int bytes)
    {
        try
        {
            return WebEncoders.Base64UrlDecode(base64Url).Length == bytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

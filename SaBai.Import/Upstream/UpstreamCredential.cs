using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SaBai.Import.Upstream;

/// <summary>
/// The upstream session the operator copied from a signed-in browser: the Supabase auth cookie(s)
/// (<c>sb-&lt;project&gt;-auth-token</c>, possibly split into <c>.0</c>, <c>.1</c>, ...). It is sent
/// to upstream exactly as given and never printed.
/// </summary>
/// <remarks>
/// Upstream (<c>src/server/context.ts</c>, <c>src/proxy.ts</c>) calls Supabase
/// <c>auth.getUser()</c> on every request. When the session in the cookie expires within 90
/// seconds (auth-js <c>EXPIRY_MARGIN_MS</c>), that call uses the refresh token, which rotates the
/// session on Supabase: a write. To make sure that never happens, the tool reads
/// <c>expires_at</c> from the cookie and refuses to send any request once the session is within
/// <see cref="MinimumLifetime"/> of expiring. It never refreshes, signs in or signs out itself.
/// </remarks>
public sealed partial class UpstreamCredential
{
    public const string EnvironmentVariable = "SABAI_IMPORT_UPSTREAM_COOKIE";

    private const string Base64Prefix = "base64-";

    private readonly Func<string?> _read;
    private readonly TimeProvider _clock;
    private string _cookieHeader = "";
    private DateTimeOffset _expiresAt;

    private UpstreamCredential(Func<string?> read, string source, TimeSpan minimumLifetime, TimeProvider clock)
    {
        _read = read;
        Source = source;
        MinimumLifetime = minimumLifetime;
        _clock = clock;
        Load();
    }

    /// <summary>Where the cookie comes from (for messages; never the value).</summary>
    public string Source { get; }

    /// <summary>Requests stop once the session has less than this left.</summary>
    public TimeSpan MinimumLifetime { get; }

    public DateTimeOffset ExpiresAt => _expiresAt;

    public static UpstreamCredential FromEnvironment(TimeSpan minimumLifetime, TimeProvider? clock = null) =>
        new(
            () => Environment.GetEnvironmentVariable(EnvironmentVariable),
            $"environment variable {EnvironmentVariable}",
            minimumLifetime,
            clock ?? TimeProvider.System
        );

    /// <summary>
    /// Reads the cookie from a file. The file is read again when the session gets close to
    /// expiring, so a long import can be kept going by pasting a fresh cookie into it.
    /// </summary>
    public static UpstreamCredential FromFile(string path, TimeSpan minimumLifetime, TimeProvider? clock = null) =>
        new(
            () => File.Exists(path) ? File.ReadAllText(path) : null,
            $"file {path}",
            minimumLifetime,
            clock ?? TimeProvider.System
        );

    public static UpstreamCredential FromValue(string cookieHeader, TimeSpan minimumLifetime, TimeProvider? clock = null) =>
        new(() => cookieHeader, "value", minimumLifetime, clock ?? TimeProvider.System);

    /// <summary>
    /// The Cookie header to send now. Throws when the session is too close to expiring for the
    /// server to use it without refreshing it.
    /// </summary>
    public string CookieHeaderForRequest()
    {
        if (!HasEnoughLifetime())
        {
            // The operator may have put a fresh cookie in the file meanwhile.
            Load();
            if (!HasEnoughLifetime())
            {
                throw new UpstreamCredentialException(
                    $"The upstream session from {Source} expires at {_expiresAt:u}, less than "
                        + $"{MinimumLifetime.TotalMinutes:0} minutes from now. Sending it could make the upstream server "
                        + "refresh it (a write on Supabase), so the import stops here. Reload the upstream app in "
                        + "your browser, copy the cookie again and re-run (the import is idempotent)."
                );
            }
        }

        return _cookieHeader;
    }

    private bool HasEnoughLifetime() => _expiresAt - _clock.GetUtcNow() >= MinimumLifetime;

    private void Load()
    {
        var raw = _read()?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            throw new UpstreamCredentialException($"No upstream cookie in {Source}.");
        }

        // Accept "Cookie: a=b; c=d" as copied from devtools, or just "a=b; c=d".
        if (raw.StartsWith("cookie:", StringComparison.OrdinalIgnoreCase))
        {
            raw = raw["cookie:".Length..].Trim();
        }

        if (raw.Contains('\n') || raw.Contains('\r'))
        {
            throw new UpstreamCredentialException($"The cookie in {Source} must be a single line.");
        }

        _expiresAt = ReadExpiry(raw, Source);
        _cookieHeader = raw;
    }

    /// <summary>Reads <c>expires_at</c> from the Supabase session in a Cookie header.</summary>
    internal static DateTimeOffset ReadExpiry(string cookieHeader, string source)
    {
        var chunks = new SortedDictionary<int, string>();
        string? whole = null;
        foreach (var part in cookieHeader.Split(';'))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var name = part[..eq].Trim();
            var value = part[(eq + 1)..].Trim();
            var match = AuthCookieName().Match(name);
            if (!match.Success)
            {
                continue;
            }

            if (match.Groups["chunk"].Success)
            {
                chunks[int.Parse(match.Groups["chunk"].Value)] = value;
            }
            else
            {
                whole = value;
            }
        }

        var encoded = whole ?? (chunks.Count > 0 ? string.Concat(chunks.Values) : null);
        if (encoded is null)
        {
            throw new UpstreamCredentialException(
                $"The cookie in {source} has no Supabase session cookie (sb-<project>-auth-token). "
                    + "Copy all sb-*-auth-token cookies of the upstream site."
            );
        }

        try
        {
            var json = encoded.StartsWith(Base64Prefix, StringComparison.Ordinal)
                ? Encoding.UTF8.GetString(Base64UrlDecode(encoded[Base64Prefix.Length..]))
                : Uri.UnescapeDataString(encoded);
            using var doc = JsonDocument.Parse(json);
            if (
                doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("expires_at", out var expires)
                && expires.TryGetInt64(out var seconds)
            )
            {
                return DateTimeOffset.FromUnixTimeSeconds(seconds);
            }
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            // Fall through to the error below.
        }

        throw new UpstreamCredentialException(
            $"Could not read the session expiry from the Supabase cookie in {source}. Without it the tool "
                + "cannot make sure the upstream server never refreshes the session, so it will not use it."
        );
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
        return Convert.FromBase64String(s);
    }

    [GeneratedRegex(@"^sb-.+-auth-token(\.(?<chunk>0|[1-9][0-9]*))?$")]
    private static partial Regex AuthCookieName();

    public override string ToString() => $"upstream cookie from {Source} (expires {_expiresAt:u})";
}

public sealed class UpstreamCredentialException(string message) : Exception(message);

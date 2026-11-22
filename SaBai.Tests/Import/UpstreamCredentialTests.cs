using System.Text;
using SaBai.Import.Upstream;

namespace SaBai.Tests.Import;

/// <summary>The tool never sends a session the upstream server would have to refresh.</summary>
public sealed class UpstreamCredentialTests
{
    private static readonly TimeSpan Minimum = TimeSpan.FromMinutes(5);

    [Test]
    public async Task ReadsExpiryFromBase64SessionCookie()
    {
        var cookie = FakeUpstream.Cookie(TimeSpan.FromMinutes(30));
        var credential = UpstreamCredential.FromValue(cookie, Minimum);

        await Assert.That(credential.CookieHeaderForRequest()).IsEqualTo(cookie);
        await Assert.That(credential.ExpiresAt).IsGreaterThan(DateTimeOffset.UtcNow.AddMinutes(29));
    }

    [Test]
    public async Task ReadsExpiryFromChunkedAndRawCookies()
    {
        var expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var json = $$"""{"access_token":"a","refresh_token":"r","expires_at":{{expires}}}""";
        var encoded = "base64-" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var chunked = $"sb-abc-auth-token.1={encoded[10..]}; sb-abc-auth-token.0={encoded[..10]}";
        var raw = $"sb-abc-auth-token={Uri.EscapeDataString(json)}";

        await Assert.That(UpstreamCredential.FromValue(chunked, Minimum).ExpiresAt.ToUnixTimeSeconds()).IsEqualTo(expires);
        await Assert.That(UpstreamCredential.FromValue("Cookie: " + raw, Minimum).ExpiresAt.ToUnixTimeSeconds()).IsEqualTo(expires);
    }

    [Test]
    public async Task RefusesAnExpiringSessionWithoutSendingIt()
    {
        var transport = new ReadOnlyGuardTests.RecordingHandler();
        var credential = UpstreamCredential.FromValue(FakeUpstream.Cookie(TimeSpan.FromMinutes(2)), Minimum);
        using var client = new UpstreamClient(new Uri("https://upstream.example.org"), credential, transport: transport);

        await Assert.ThrowsAsync<UpstreamCredentialException>(() =>
            client.QueryAsync<object>(UpstreamClient.Procedures.PatientsList, null, CancellationToken.None)
        );
        await Assert.That(transport.Requests).IsEmpty();
    }

    [Test]
    [Arguments("")]
    [Arguments("theme=dark")]
    [Arguments("sb-abc-auth-token=base64-bm90IGpzb24")]
    [Arguments("sb-abc-auth-token=%7B%22access_token%22%3A%22a%22%7D")]
    public async Task RefusesCookiesWithoutAReadableSessionExpiry(string cookie)
    {
        await Assert.ThrowsAsync<UpstreamCredentialException>(() =>
            Task.FromResult(UpstreamCredential.FromValue(cookie, Minimum))
        );
    }

    [Test]
    public async Task FileCredentialIsReReadWhenItNearsExpiry()
    {
        var path = Path.GetTempFileName();
        try
        {
            var now = new MutableClock(DateTimeOffset.UtcNow);
            await File.WriteAllTextAsync(path, FakeUpstream.Cookie(TimeSpan.FromMinutes(10)));
            var credential = UpstreamCredential.FromFile(path, Minimum, now);
            var first = credential.CookieHeaderForRequest();

            // Six minutes later the session has four left: too close. A fresh cookie in the file is picked up.
            now.Now = now.Now.AddMinutes(6);
            var fresh = FakeUpstream.Cookie(TimeSpan.FromHours(1));
            await File.WriteAllTextAsync(path, fresh);

            await Assert.That(credential.CookieHeaderForRequest()).IsEqualTo(fresh);
            await Assert.That(first).IsNotEqualTo(fresh);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}

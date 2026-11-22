using System.Net;

namespace SaBai.Import.Upstream;

/// <summary>
/// Downloads photos (Cloudinary delivery URLs) and patient files (upstream's presigned S3 GET
/// links) with plain GETs through <see cref="ReadOnlyGuardHandler"/>. No credentials are sent:
/// Cloudinary delivery URLs of images uploaded with the default "upload" type are public, and
/// presigned links carry their own signature. It refuses the upstream app host, which only
/// <see cref="UpstreamClient"/> may talk to.
/// </summary>
public sealed class MediaDownloader : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _upstreamHost;
    private readonly SemaphoreSlim _gate;
    private readonly int _maxAttempts;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public MediaDownloader(
        Uri upstreamBaseUrl,
        int concurrency = 4,
        int maxAttempts = 4,
        HttpMessageHandler? transport = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null
    )
    {
        _upstreamHost = upstreamBaseUrl.Authority;
        _gate = new SemaphoreSlim(Math.Max(1, concurrency));
        _maxAttempts = Math.Max(1, maxAttempts);
        _delay = delay ?? Task.Delay;
        _http = ReadOnlyGuardHandler.CreateClient(TimeSpan.FromMinutes(2), transport);
    }

    public async Task<byte[]> GetAsync(Uri url, long maxBytes, CancellationToken ct)
    {
        if (url.Scheme is not ("https" or "http"))
        {
            throw new MediaDownloadException($"Refusing to download a non-http(s) URL ({url.Scheme}).");
        }

        if (string.Equals(url.Authority, _upstreamHost, StringComparison.OrdinalIgnoreCase))
        {
            throw new ReadOnlyViolationException(
                "Refused to download from the upstream app host: only the upstream client may call it."
            );
        }

        await _gate.WaitAsync(ct);
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                    if (IsTransient(response.StatusCode) && attempt < _maxAttempts)
                    {
                        await _delay(Backoff(attempt), ct);
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new MediaDownloadException(
                            $"GET {url.GetLeftPart(UriPartial.Path)} answered HTTP {(int)response.StatusCode}."
                        );
                    }

                    if (response.Content.Headers.ContentLength > maxBytes)
                    {
                        throw new MediaDownloadException($"File is larger than {maxBytes} bytes.");
                    }

                    await using var body = await response.Content.ReadAsStreamAsync(ct);
                    using var buffer = new MemoryStream();
                    var chunk = new byte[81920];
                    int read;
                    while ((read = await body.ReadAsync(chunk, ct)) > 0)
                    {
                        if (buffer.Length + read > maxBytes)
                        {
                            throw new MediaDownloadException($"File is larger than {maxBytes} bytes.");
                        }

                        buffer.Write(chunk, 0, read);
                    }

                    return buffer.ToArray();
                }
                catch (HttpRequestException) when (attempt < _maxAttempts)
                {
                    await _delay(Backoff(attempt), ct);
                }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < _maxAttempts)
                {
                    await _delay(Backoff(attempt), ct);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
            or HttpStatusCode.InternalServerError;

    private static TimeSpan Backoff(int attempt) =>
        TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1) + Random.Shared.Next(0, 250));

    public void Dispose()
    {
        _http.Dispose();
        _gate.Dispose();
    }
}

public sealed class MediaDownloadException(string message) : Exception(message);

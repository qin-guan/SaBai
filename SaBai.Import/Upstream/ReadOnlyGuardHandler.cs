namespace SaBai.Import.Upstream;

/// <summary>
/// Sits under every <see cref="HttpClient"/> this tool uses to reach an outside host (the upstream
/// tRPC server, Cloudinary, upstream's S3 bucket). Anything but a GET is refused before it leaves
/// the process, so no request that could change data can be sent, even by a coding mistake.
/// Redirects are not followed either (the handler below it is created with redirects off), so a
/// server cannot bounce a request somewhere else.
/// </summary>
public sealed class ReadOnlyGuardHandler : DelegatingHandler
{
    public ReadOnlyGuardHandler() { }

    public ReadOnlyGuardHandler(HttpMessageHandler inner)
        : base(inner) { }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        Check(request);
        return base.SendAsync(request, cancellationToken);
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Check(request);
        return base.Send(request, cancellationToken);
    }

    private static void Check(HttpRequestMessage request)
    {
        if (request.Method != HttpMethod.Get)
        {
            throw new ReadOnlyViolationException(
                $"Refused to send {request.Method} {request.RequestUri?.GetLeftPart(UriPartial.Path)}: "
                    + "the import only ever reads from outside hosts (GET)."
            );
        }

        if (request.Content is not null)
        {
            throw new ReadOnlyViolationException(
                $"Refused to send a GET with a body to {request.RequestUri?.GetLeftPart(UriPartial.Path)}."
            );
        }
    }

    /// <summary>A guarded client: GET only, no redirects, no cookie container.</summary>
    public static HttpClient CreateClient(TimeSpan timeout, HttpMessageHandler? transport = null) =>
        new(
            new ReadOnlyGuardHandler(
                transport
                    ?? new SocketsHttpHandler
                    {
                        AllowAutoRedirect = false,
                        UseCookies = false,
                        AutomaticDecompression = System.Net.DecompressionMethods.All,
                    }
            )
        )
        {
            Timeout = timeout,
        };
}

/// <summary>Something tried to send a request that is not a plain GET to an outside host.</summary>
public sealed class ReadOnlyViolationException(string message) : Exception(message);

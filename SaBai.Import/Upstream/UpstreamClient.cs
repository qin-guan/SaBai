using System.Net;
using System.Text.Json;

namespace SaBai.Import.Upstream;

/// <summary>
/// The only code that talks to the upstream (project-sabai) server. It can call only the tRPC
/// procedures in <see cref="AllowedQueries"/>, each one checked against upstream's router source
/// to be a <c>.query(...)</c> whose resolver only selects rows. Requests are plain GETs
/// (<c>GET /api/trpc/&lt;procedure&gt;?input=&lt;superjson&gt;</c>, tRPC v11 non-batched HTTP),
/// through <see cref="ReadOnlyGuardHandler"/>; upstream's tRPC server itself also never runs a
/// mutation for a GET.
/// </summary>
public sealed class UpstreamClient : IDisposable
{
    /// <summary>
    /// Upstream query procedures this tool may call. Verified against
    /// <c>src/server/routers/*.ts</c> of NUS-Project-SaBai/project-sabai (dev, a233797): each is
    /// <c>protectedProcedure...query(...)</c> and its resolver only runs SELECTs.
    /// <c>filesRouter.listByPatientId</c> also presigns S3 GET links with the AWS SDK, which is a
    /// local signature computation (no AWS call, nothing stored).
    /// </summary>
    public static readonly IReadOnlySet<string> AllowedQueries = new HashSet<string>(StringComparer.Ordinal)
    {
        Procedures.VillageCodesList,
        Procedures.PatientsList,
        Procedures.VisitsList,
        Procedures.VitalsByVisit,
        Procedures.PubertyByVitals,
        Procedures.EyesightByVisit,
        Procedures.ConsultsByVisit,
        Procedures.ReferralsList,
        Procedures.ReferralPartnersList,
        Procedures.IngredientsList,
        Procedures.BrandsList,
        Procedures.StockList,
        Procedures.BroadcastsList,
        Procedures.FilesByPatient,
    };

    public static class Procedures
    {
        public const string VillageCodesList = "villageCodesRouter.list";
        public const string PatientsList = "patientsRouter.list";
        public const string VisitsList = "visitsRouter.list";
        public const string VitalsByVisit = "vitalsRouter.getByVisitId";
        public const string PubertyByVitals = "pubertyRouter.getByVitalId";
        public const string EyesightByVisit = "eyesightRouter.getByVisitId";
        public const string ConsultsByVisit = "consultsRouter.getByVisitId";
        public const string ReferralsList = "referralRouter.list";
        public const string ReferralPartnersList = "referralPartnersRouter.list";
        public const string IngredientsList = "medicationActiveIngredientsRouter.list";
        public const string BrandsList = "medicationBrandRouter.list";
        public const string StockList = "medicationStockRouter.list";
        public const string BroadcastsList = "broadcastsRouter.list";
        public const string FilesByPatient = "filesRouter.listByPatientId";
    }

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly Uri _trpcBase;
    private readonly UpstreamCredential _credential;
    private readonly SemaphoreSlim _gate;
    private readonly int _maxAttempts;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    /// <param name="baseUrl">The upstream site, e.g. <c>https://sabai.example.org</c>.</param>
    /// <param name="transport">Test seam: the handler under the read-only guard.</param>
    public UpstreamClient(
        Uri baseUrl,
        UpstreamCredential credential,
        int concurrency = 4,
        int maxAttempts = 4,
        HttpMessageHandler? transport = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null
    )
    {
        if (!baseUrl.IsAbsoluteUri || baseUrl.Scheme is not ("https" or "http"))
        {
            throw new ArgumentException("The upstream URL must be an absolute http(s) URL.", nameof(baseUrl));
        }

        BaseUrl = baseUrl;
        _trpcBase = new Uri(baseUrl, "/api/trpc/");
        _credential = credential;
        _gate = new SemaphoreSlim(Math.Max(1, concurrency));
        _maxAttempts = Math.Max(1, maxAttempts);
        _delay = delay ?? Task.Delay;
        _http = ReadOnlyGuardHandler.CreateClient(TimeSpan.FromSeconds(60), transport);
    }

    public Uri BaseUrl { get; }

    /// <summary>Number of HTTP requests sent (attempts included).</summary>
    public int RequestCount => _requests;

    private int _requests;

    /// <summary>
    /// Calls an allowlisted query procedure and returns the deserialized <c>json</c> part of its
    /// superjson-encoded result.
    /// </summary>
    /// <exception cref="ReadOnlyViolationException">The procedure is not an allowlisted query.</exception>
    public async Task<T> QueryAsync<T>(string procedure, object? input, CancellationToken ct)
    {
        var uri = BuildUri(procedure, input);
        var element = await SendAsync(uri, procedure, ct);
        return element.Deserialize<T>(Json)!;
    }

    /// <summary>The request URI for a call; refuses anything not on the allowlist.</summary>
    public Uri BuildUri(string procedure, object? input)
    {
        if (!AllowedQueries.Contains(procedure))
        {
            throw new ReadOnlyViolationException(
                $"Refused to call upstream procedure '{procedure}': it is not on the read-only query allowlist."
            );
        }

        var path = Uri.EscapeDataString(procedure).Replace("%2E", ".", StringComparison.OrdinalIgnoreCase);
        if (input is null)
        {
            return new Uri(_trpcBase, path);
        }

        // The upstream router uses the superjson transformer: input is {"json": <value>}. None of
        // the inputs sent here contain Dates, undefined or other values that would need "meta".
        var encoded = JsonSerializer.Serialize(new { json = input }, Json);
        return new Uri(_trpcBase, $"{path}?input={Uri.EscapeDataString(encoded)}");
    }

    private async Task<JsonElement> SendAsync(Uri uri, string procedure, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                // Checked before every attempt: never send a session the server would refresh.
                var cookie = _credential.CookieHeaderForRequest();
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.TryAddWithoutValidation("Cookie", cookie);
                request.Headers.Accept.ParseAdd("application/json");
                Interlocked.Increment(ref _requests);

                HttpResponseMessage response;
                try
                {
                    response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
                }
                catch (HttpRequestException ex) when (attempt < _maxAttempts)
                {
                    await BackoffAsync(attempt, $"{procedure}: {ex.Message}", ct);
                    continue;
                }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < _maxAttempts)
                {
                    await BackoffAsync(attempt, $"{procedure}: timed out", ct);
                    continue;
                }

                using (response)
                {
                    var status = (int)response.StatusCode;
                    if (status is >= 300 and < 400)
                    {
                        throw new UpstreamException(
                            procedure,
                            response.StatusCode,
                            "UNAUTHORIZED",
                            "upstream redirected the request (usually to /login): the session cookie was not accepted."
                        );
                    }

                    if (IsTransient(response.StatusCode) && attempt < _maxAttempts)
                    {
                        await BackoffAsync(attempt, $"{procedure}: HTTP {status}", ct);
                        continue;
                    }

                    var body = await response.Content.ReadAsStringAsync(ct);
                    return Unwrap(procedure, response.StatusCode, body);
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

    private async Task BackoffAsync(int attempt, string reason, CancellationToken ct)
    {
        var wait = TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1) + Random.Shared.Next(0, 250));
        Console.Error.WriteLine($"  retrying {reason} in {wait.TotalSeconds:0.0}s (attempt {attempt + 1}/{_maxAttempts})");
        await _delay(wait, ct);
    }

    /// <summary>
    /// Reads a tRPC v11 non-batched response: <c>{"result":{"data":{"json":...,"meta":...}}}</c>
    /// or <c>{"error":{"json":{"message","code","data":{"code","httpStatus"}}}}</c>.
    /// </summary>
    internal static JsonElement Unwrap(string procedure, HttpStatusCode status, string body)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw new UpstreamException(procedure, status, null, $"response was not JSON (HTTP {(int)status}).");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var error))
            {
                var shape = error.TryGetProperty("json", out var json) ? json : error;
                var message = shape.TryGetProperty("message", out var m) ? m.GetString() : null;
                string? code = null;
                if (shape.TryGetProperty("data", out var data) && data.TryGetProperty("code", out var c))
                {
                    code = c.GetString();
                }

                throw new UpstreamException(procedure, status, code, message ?? "unknown error");
            }

            if (
                root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("result", out var result)
                && result.TryGetProperty("data", out var payload)
            )
            {
                // superjson: {"json": value, "meta": {...}}. Dates arrive as ISO strings in "json".
                var value = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("json", out var j)
                    ? j
                    : payload;
                return value.Clone();
            }

            throw new UpstreamException(procedure, status, null, "response had neither result nor error.");
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        _gate.Dispose();
    }
}

public sealed class UpstreamException(string procedure, HttpStatusCode status, string? code, string message)
    : Exception($"{procedure} failed (HTTP {(int)status}{(code is null ? "" : $" {code}")}): {message}")
{
    public string Procedure { get; } = procedure;

    public HttpStatusCode Status { get; } = status;

    public string? Code { get; } = code;
}

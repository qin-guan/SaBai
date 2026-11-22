using System.Net.Http.Json;
using SaBai.Import.Upstream;

namespace SaBai.Tests.Import;

/// <summary>The import can never send anything but a GET query to an outside host.</summary>
public sealed class ReadOnlyGuardTests
{
    private static readonly Uri Upstream = new("https://upstream.example.org");

    [Test]
    [Arguments("POST")]
    [Arguments("PUT")]
    [Arguments("PATCH")]
    [Arguments("DELETE")]
    [Arguments("HEAD")]
    [Arguments("OPTIONS")]
    public async Task Guard_RefusesEveryNonGetRequest(string method)
    {
        var transport = new RecordingHandler();
        using var client = ReadOnlyGuardHandler.CreateClient(TimeSpan.FromSeconds(5), transport);

        await Assert.ThrowsAsync<ReadOnlyViolationException>(() =>
            client.SendAsync(new HttpRequestMessage(new HttpMethod(method), "https://upstream.example.org/api/trpc/patientsRouter.create"))
        );
        await Assert.That(transport.Requests).IsEmpty();
    }

    [Test]
    public async Task Guard_RefusesPostHelpersAndGetWithBody()
    {
        var transport = new RecordingHandler();
        using var client = ReadOnlyGuardHandler.CreateClient(TimeSpan.FromSeconds(5), transport);

        await Assert.ThrowsAsync<ReadOnlyViolationException>(() =>
            client.PostAsJsonAsync("https://res.cloudinary.com/demo/image/upload", new { file = "x" })
        );
        await Assert.ThrowsAsync<ReadOnlyViolationException>(() =>
            client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://upstream.example.org/x") { Content = new StringContent("{}") })
        );
        await Assert.That(transport.Requests).IsEmpty();
    }

    [Test]
    public async Task Guard_LetsPlainGetThrough()
    {
        var transport = new RecordingHandler();
        using var client = ReadOnlyGuardHandler.CreateClient(TimeSpan.FromSeconds(5), transport);

        using var response = await client.GetAsync("https://upstream.example.org/ok");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(transport.Requests.Single().Method).IsEqualTo(HttpMethod.Get);
    }

    [Test]
    [Arguments("patientsRouter.create")]
    [Arguments("patientsRouter.update")]
    [Arguments("patientsRouter.delete")]
    [Arguments("patientsRouter.searchPatientsByPicture")]
    [Arguments("ordersRouter.create")]
    [Arguments("filesRouter.createUploadUrl")]
    [Arguments("broadcastsRouter.send")]
    [Arguments("visitsRouter.hasRecentVisit")]
    [Arguments("villageCodesRouter.list,patientsRouter.delete")]
    [Arguments("../login")]
    public async Task Client_RefusesProceduresNotOnTheAllowlist(string procedure)
    {
        var transport = new RecordingHandler();
        using var client = new UpstreamClient(Upstream, Credential(), transport: transport);

        await Assert.ThrowsAsync<ReadOnlyViolationException>(() =>
            client.QueryAsync<object>(procedure, new { id = 1 }, CancellationToken.None)
        );
        await Assert.That(transport.Requests).IsEmpty();
    }

    [Test]
    public async Task Client_SendsTrpcGetWithSuperjsonInputAndTheCookieAsIs()
    {
        var transport = new RecordingHandler(
            """{"result":{"data":{"json":[{"id":1,"code":"V1"}],"meta":{"v":1}}}}"""
        );
        var cookie = FakeUpstream.Cookie(TimeSpan.FromHours(1));
        using var client = new UpstreamClient(
            Upstream,
            UpstreamCredential.FromValue(cookie, TimeSpan.FromMinutes(5)),
            transport: transport
        );

        var rows = await client.QueryAsync<List<Dictionary<string, object>>>(
            UpstreamClient.Procedures.VillageCodesList,
            new { includeHidden = true },
            CancellationToken.None
        );

        var request = transport.Requests.Single();
        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(request.Method).IsEqualTo(HttpMethod.Get);
        await Assert.That(request.Uri.GetLeftPart(UriPartial.Path))
            .IsEqualTo("https://upstream.example.org/api/trpc/villageCodesRouter.list");
        // Non-batched tRPC v11 GET: ?input=<JSON of the superjson-serialized input>, no batch=1.
        await Assert.That(Uri.UnescapeDataString(request.Uri.Query))
            .IsEqualTo("""?input={"json":{"includeHidden":true}}""");
        await Assert.That(request.Cookie).IsEqualTo(cookie);
    }

    [Test]
    public async Task Client_SurfacesTrpcErrorsAndRedirects()
    {
        var error = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Import", "Fixtures", "unauthorized.response.json"));
        using (var client = new UpstreamClient(Upstream, Credential(), transport: new RecordingHandler(error, HttpStatusCode.Unauthorized)))
        {
            var ex = await Assert.ThrowsAsync<UpstreamException>(() =>
                client.QueryAsync<object>(UpstreamClient.Procedures.PatientsList, null, CancellationToken.None)
            );
            await Assert.That(ex!.Code).IsEqualTo("UNAUTHORIZED");
        }

        using (var client = new UpstreamClient(Upstream, Credential(), transport: new RecordingHandler("", HttpStatusCode.TemporaryRedirect)))
        {
            await Assert.ThrowsAsync<UpstreamException>(() =>
                client.QueryAsync<object>(UpstreamClient.Procedures.PatientsList, null, CancellationToken.None)
            );
        }
    }

    [Test]
    public async Task Client_RetriesTransientFailuresWithGetsOnly()
    {
        var transport = new RecordingHandler("""{"result":{"data":{"json":[]}}}""", HttpStatusCode.ServiceUnavailable, failures: 2);
        using var client = new UpstreamClient(Upstream, Credential(), transport: transport, delay: (_, _) => Task.CompletedTask);

        await client.QueryAsync<List<object>>(UpstreamClient.Procedures.PatientsList, null, CancellationToken.None);

        await Assert.That(transport.Requests.Count).IsEqualTo(3);
        await Assert.That(transport.Requests.All(x => x.Method == HttpMethod.Get)).IsTrue();
    }

    [Test]
    public async Task Allowlist_HoldsOnlyVerifiedQueries()
    {
        await Assert.That(UpstreamClient.AllowedQueries.Order().ToArray()).IsEquivalentTo(new[]
        {
            "broadcastsRouter.list",
            "consultsRouter.getByVisitId",
            "eyesightRouter.getByVisitId",
            "filesRouter.listByPatientId",
            "medicationActiveIngredientsRouter.list",
            "medicationBrandRouter.list",
            "medicationStockRouter.list",
            "patientsRouter.list",
            "pubertyRouter.getByVitalId",
            "referralPartnersRouter.list",
            "referralRouter.list",
            "villageCodesRouter.list",
            "visitsRouter.list",
            "vitalsRouter.getByVisitId",
        });
    }

    [Test]
    public async Task MediaDownloader_RefusesTheUpstreamAppHost()
    {
        var transport = new RecordingHandler();
        using var downloader = new MediaDownloader(Upstream, transport: transport);

        await Assert.ThrowsAsync<ReadOnlyViolationException>(() =>
            downloader.GetAsync(new Uri("https://upstream.example.org/api/trpc/patientsRouter.list"), 1000, CancellationToken.None)
        );
        await Assert.That(transport.Requests).IsEmpty();
    }

    [Test]
    public async Task MediaDownloader_SendsNoCredentialsAndCapsSize()
    {
        var transport = new RecordingHandler(new string('x', 2000));
        using var downloader = new MediaDownloader(Upstream, transport: transport);

        await Assert.ThrowsAsync<MediaDownloadException>(() =>
            downloader.GetAsync(new Uri("https://res.cloudinary.com/demo/image/upload/v1/a"), 1000, CancellationToken.None)
        );
        await Assert.That(transport.Requests.Single().Cookie).IsNull();
    }

    private static UpstreamCredential Credential() =>
        UpstreamCredential.FromValue(FakeUpstream.Cookie(TimeSpan.FromHours(1)), TimeSpan.FromMinutes(5));

    internal sealed class RecordingHandler(string body = "{}", HttpStatusCode status = HttpStatusCode.OK, int failures = 0)
        : HttpMessageHandler
    {
        public List<(HttpMethod Method, Uri Uri, string? Cookie)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((
                request.Method,
                request.RequestUri!,
                request.Headers.TryGetValues("Cookie", out var cookies) ? string.Join("; ", cookies) : null
            ));
            var code = failures > 0 && Requests.Count <= failures ? status : failures > 0 ? HttpStatusCode.OK : status;
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) });
        }
    }
}

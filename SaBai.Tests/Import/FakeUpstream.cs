using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SaBai.Tests.Support;

namespace SaBai.Tests.Import;

/// <summary>
/// A stand-in for a project-sabai deployment: answers tRPC v11 non-batched GETs
/// (<c>/api/trpc/&lt;procedure&gt;?input={"json":...}</c>, superjson envelope) from the fixture
/// dataset, and records every request so tests can prove only GET queries were sent.
/// </summary>
public sealed class FakeUpstream : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly JsonObject _dataset;

    public ConcurrentQueue<(string Method, string Path, string Query, string? Cookie)> Requests { get; } = new();

    private FakeUpstream(WebApplication app, JsonObject dataset)
    {
        _app = app;
        _dataset = dataset;
    }

    public Uri BaseUrl { get; private set; } = null!;

    public JsonObject Dataset => _dataset;

    public static async Task<FakeUpstream> StartAsync(JsonObject dataset)
    {
        var app = CreateApp();
        var fake = new FakeUpstream(app, dataset);
        app.Run(fake.HandleAsync);
        await app.StartAsync();
        fake.BaseUrl = Address(app);
        return fake;
    }

    /// <summary>Loads the fixture dataset, pointing image and file links at <paramref name="imageHost"/>.</summary>
    public static JsonObject LoadDataset(Uri imageHost)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Import", "Fixtures", "upstream-dataset.json");
        var text = File.ReadAllText(path).Replace("{IMAGE_HOST}", imageHost.ToString().TrimEnd('/'));
        return JsonNode.Parse(text)!.AsObject();
    }

    private async Task HandleAsync(HttpContext http)
    {
        Requests.Enqueue((
            http.Request.Method,
            http.Request.Path.Value ?? "",
            http.Request.QueryString.Value ?? "",
            http.Request.Headers.Cookie.ToString()
        ));
        if (!HttpMethods.IsGet(http.Request.Method))
        {
            http.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        var path = http.Request.Path.Value ?? "";
        const string prefix = "/api/trpc/";
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!http.Request.Headers.Cookie.ToString().Contains("sb-fake-auth-token", StringComparison.Ordinal))
        {
            await WriteAsync(http, 401, Error("UNAUTHORIZED", 401));
            return;
        }

        var procedure = path[prefix.Length..];
        if (!_dataset.TryGetPropertyValue(procedure, out var node))
        {
            await WriteAsync(http, 404, Error("NOT_FOUND", 404));
            return;
        }

        // superjson input: {"json": {...}}, as tRPC's transformer.input.deserialize expects.
        JsonObject? input = null;
        if (http.Request.Query.TryGetValue("input", out var raw))
        {
            input = JsonNode.Parse(raw.ToString())!.AsObject()["json"]?.AsObject();
        }

        JsonNode? result = node;
        if (node is JsonObject byId)
        {
            var key = input?.FirstOrDefault(x => x.Key.EndsWith("Id", StringComparison.Ordinal)).Value?.ToString();
            result = key is not null && byId.TryGetPropertyValue(key, out var value) ? value : null;
        }

        await WriteAsync(http, 200, new JsonObject
        {
            ["result"] = new JsonObject
            {
                ["data"] = new JsonObject { ["json"] = result?.DeepClone(), ["meta"] = new JsonObject { ["v"] = 1 } },
            },
        });
    }

    private static JsonObject Error(string code, int status) =>
        new()
        {
            ["error"] = new JsonObject
            {
                ["json"] = new JsonObject
                {
                    ["message"] = code,
                    ["code"] = -32001,
                    ["data"] = new JsonObject { ["code"] = code, ["httpStatus"] = status },
                },
            },
        };

    private static async Task WriteAsync(HttpContext http, int status, JsonNode body)
    {
        http.Response.StatusCode = status;
        http.Response.ContentType = "application/json";
        await http.Response.WriteAsync(body.ToJsonString());
    }

    internal static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        return builder.Build();
    }

    internal static Uri Address(WebApplication app) =>
        new(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First());

    /// <summary>A Cookie header holding a Supabase session that expires in <paramref name="lifetime"/>.</summary>
    public static string Cookie(TimeSpan lifetime)
    {
        var session = JsonSerializer.Serialize(new
        {
            access_token = "header.payload.signature",
            token_type = "bearer",
            expires_in = (int)lifetime.TotalSeconds,
            expires_at = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds(),
            refresh_token = "refresh",
            user = new { id = "00000000-0000-0000-0000-000000000001" },
        });
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(session))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"theme=dark; sb-fake-auth-token=base64-{encoded}";
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}

/// <summary>Stands in for Cloudinary and upstream's S3: serves photos and files over GET.</summary>
public sealed class FakeMediaHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    public ConcurrentQueue<(string Method, string Path, string? Cookie)> Requests { get; } = new();

    private FakeMediaHost(WebApplication app) => _app = app;

    public Uri BaseUrl { get; private set; } = null!;

    public static async Task<FakeMediaHost> StartAsync()
    {
        var app = FakeUpstream.CreateApp();
        var host = new FakeMediaHost(app);
        app.Run(async http =>
        {
            var path = http.Request.Path.Value ?? "";
            host.Requests.Enqueue((http.Request.Method, path, http.Request.Headers.Cookie.ToString()));
            if (!HttpMethods.IsGet(http.Request.Method))
            {
                http.Response.StatusCode = 405;
                return;
            }

            (byte[] Bytes, string Type)? body = path switch
            {
                "/demo/image/upload/v1700000001/p710001" => (SampleFiles.Png(), "image/png"),
                "/demo/image/upload/v1700000002/p710002" => (SampleFiles.Jpeg(), "image/jpeg"),
                _ when path.StartsWith("/s3/patients/710001/", StringComparison.Ordinal) => (SampleFiles.Pdf, "application/pdf"),
                _ => null,
            };
            if (body is null)
            {
                http.Response.StatusCode = 404;
                return;
            }

            http.Response.ContentType = body.Value.Type;
            await http.Response.Body.WriteAsync(body.Value.Bytes);
        });
        await app.StartAsync();
        host.BaseUrl = FakeUpstream.Address(app);
        return host;
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}

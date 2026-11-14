using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace SaBai.Tests.Telemetry;

/// <summary>
/// Runs a small app with the shared service defaults on Kestrel and checks what the telemetry
/// pipeline would export: no span or log record may contain a patient's name from the URL.
/// </summary>
public sealed class TelemetryRedactionTests
{
    private const string Name = "Somchai";

    [Test]
    public async Task SearchByName_DoesNotPutTheNameOnSpansOrLogs()
    {
        // Arrange
        var spans = new ConcurrentQueue<Activity>();
        var logs = new ConcurrentQueue<LogRecordSnapshot>();
        await using var app = await StartAppAsync(spans, logs);
        using var client = new HttpClient { BaseAddress = BaseAddress(app) };

        // Act
        var search = await client.GetAsync($"/fhir/r4/Patient?name={Name}&_count=5");
        var person = await client.GetAsync($"/persons/{Name}/faces");
        var byId = await client.GetAsync($"/persons/{Guid.Empty}/faces");
        await WaitForAsync(() => spans.Count(s => s.Kind == ActivityKind.Server) >= 3);

        // Assert
        await Assert.That(search.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(person.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(byId.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var tags = spans
            .SelectMany(s => s.TagObjects.Select(t => $"{s.Kind} {s.DisplayName} {t.Key}={t.Value}"))
            .ToList();
        // No span carries the query; server spans carry no name at all (this test's own client
        // span for /persons/{name} keeps its path, as the API never calls out with names).
        await Assert
            .That(tags.Where(t => t.Contains($"={Name}", StringComparison.OrdinalIgnoreCase)).ToList())
            .IsEmpty();
        await Assert
            .That(
                tags.Where(t =>
                        t.StartsWith(nameof(ActivityKind.Server))
                        && t.Contains(Name, StringComparison.OrdinalIgnoreCase)
                    )
                    .ToList()
            )
            .IsEmpty();
        await Assert.That(tags.Where(t => t.Contains("url.query")).ToList()).IsEmpty();
        await Assert.That(tags.Any(t => t.EndsWith("url.path=/persons/{personId}/faces"))).IsTrue();
        await Assert.That(tags.Any(t => t.EndsWith($"url.path=/persons/{Guid.Empty}/faces"))).IsTrue();
        await Assert
            .That(tags.Any(t => t.EndsWith($"url.full={BaseAddress(app)}fhir/r4/Patient")))
            .IsTrue();

        await Assert.That(logs.Any(l => l.Template == "Searched patients")).IsTrue();
        await Assert
            .That(logs.Where(l => l.Text.Contains(Name, StringComparison.OrdinalIgnoreCase)).ToList())
            .IsEmpty();
        await Assert.That(logs.Where(l => l.FormattedMessage is not null).ToList()).IsEmpty();
    }

    [Test]
    [Arguments("/patients/0190a000-0000-7000-8000-000000000001", "/patients/{Id}", true)]
    [Arguments("/persons/Somchai Jaidee/faces", "/persons/{personId}/faces", false)]
    [Arguments("/persons/1234567890123/faces", "/persons/{personId}/faces", false)]
    [Arguments("/fhir/r4/Patient", "/fhir/r4/Patient", true)]
    public async Task HasOnlyIdParameters_KeepsOnlyGuidPaths(string path, string route, bool kept)
    {
        await Assert.That(UrlRedactionProcessor.HasOnlyIdParameters(path, route)).IsEqualTo(kept);
    }

    private static async Task<WebApplication> StartAppAsync(
        ConcurrentQueue<Activity> spans,
        ConcurrentQueue<LogRecordSnapshot> logs
    )
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        // Request logs would carry the full URL if they reached the exporter.
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Information);
        builder.AddServiceDefaults();
        builder
            .Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddProcessor(new Capture<Activity>(spans.Enqueue)));
        builder.Logging.AddOpenTelemetry(logging =>
            logging.AddProcessor(new Capture<LogRecord>(r => logs.Enqueue(new LogRecordSnapshot(r))))
        );

        var app = builder.Build();
        app.MapGet(
            "/fhir/r4/Patient",
            (string name, ILogger<TelemetryRedactionTests> logger) =>
            {
                logger.LogInformation("Searched patients");
                return Results.Ok();
            }
        );
        app.MapGet("/persons/{personId}/faces", (string personId) => Results.Ok());
        await app.StartAsync();
        return app;
    }

    private static Uri BaseAddress(WebApplication app) =>
        new(
            app.Services.GetRequiredService<IServer>()
                .Features.GetRequiredFeature<IServerAddressesFeature>()
                .Addresses.First()
                + "/"
        );

    /// <summary>Server spans end just after the response is sent.</summary>
    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
    }

    private sealed class Capture<T>(Action<T> onEnd) : BaseProcessor<T>
        where T : class
    {
        public override void OnEnd(T data) => onEnd(data);
    }

    /// <summary>What an exporter would read from a log record (records are reused after export).</summary>
    private sealed record LogRecordSnapshot(string? Template, string? FormattedMessage, string Text)
    {
        public LogRecordSnapshot(LogRecord record)
            : this(
                record.Body,
                record.FormattedMessage,
                string.Join(
                    " ",
                    new[] { record.Body, record.FormattedMessage }.Concat(
                        record.Attributes?.Select(a => $"{a.Key}={a.Value}") ?? []
                    )
                )
            ) { }
    }
}

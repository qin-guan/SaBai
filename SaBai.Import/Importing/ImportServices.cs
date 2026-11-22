using Amazon.S3;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SaBai.API.Data;
using SaBai.API.FaceRecognition;
using SaBai.API.Storage;

namespace SaBai.Import.Importing;

/// <summary>
/// The SaBai services the import writes through, configured exactly as the API configures them
/// (same configuration keys): Postgres via <c>ConnectionStrings:sabai</c>, RustFS via
/// <c>ConnectionStrings:fs-sabai</c> (or the <c>Storage</c> section), Qdrant via
/// <c>ConnectionStrings:qdrant</c>, face models via the <c>FaceRecognition</c> section.
/// </summary>
public static class ImportServices
{
    public static IHost Build(string[] configurationArgs, IDictionary<string, string?>? overrides = null)
    {
        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                Args = configurationArgs,
                ApplicationName = typeof(ImportServices).Assembly.GetName().Name,
                ContentRootPath = AppContext.BaseDirectory,
            }
        );
        // Quiet by default (the API's appsettings.json, copied alongside, logs at Information).
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft"] = "Warning",
            }
        );
        builder.Configuration.AddEnvironmentVariables();
        builder.Configuration.AddCommandLine(configurationArgs);
        if (overrides is not null)
        {
            builder.Configuration.AddInMemoryCollection(overrides);
        }

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(o => o.SingleLine = true);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // Same audit hook as the API (Program.cs): rows written by the import are audited.
        var audit = new AuditInterceptor(new HttpContextAccessor(), TimeProvider.System);
        builder.AddNpgsqlDbContext<AppDbContext>(
            connectionName: "sabai",
            configureSettings: s => s.DisableHealthChecks = true,
            configureDbContextOptions: options => options.AddInterceptors(audit)
        );
        builder.Services.AddSingleton(sp => RustFsS3.Read(sp.GetRequiredService<IConfiguration>()));
        builder.Services.AddSingleton<IAmazonS3>(sp => RustFsS3.CreateClient(sp.GetRequiredService<RustFsSettings>()));
        builder.AddQdrantClient("qdrant", s => s.DisableHealthChecks = true);
        builder.AddFaceRecognition();
        // The API's background initialiser is not wanted here; the face store initialises on first use.
        var initializer = builder.Services.FirstOrDefault(x =>
            x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(FaceRecognitionInitializer)
        );
        if (initializer is not null)
        {
            builder.Services.Remove(initializer);
        }

        return builder.Build();
    }
}

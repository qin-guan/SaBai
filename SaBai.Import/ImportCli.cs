using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaBai.API.FaceRecognition;
using SaBai.Import.Importing;
using SaBai.Import.Upstream;

namespace SaBai.Import;

/// <summary>
/// <c>sabai-import data|media|all [options]</c>: backfills a SaBai database from a running
/// project-sabai deployment through its read-only tRPC queries, then copies photos, faces and
/// files. See README.md.
/// </summary>
public static class ImportCli
{
    public const string UpstreamUrlVariable = "SABAI_IMPORT_UPSTREAM_URL";
    public const string CloudinaryPrefixVariable = "SABAI_IMPORT_CLOUDINARY_URL_PREFIX";

    public static async Task<int> Main(string[] args)
    {
        ImportOptions options;
        try
        {
            options = ImportOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(ImportOptions.Usage);
            return 64;
        }

        if (options.Help)
        {
            Console.WriteLine(ImportOptions.Usage);
            return 0;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            return await RunAsync(options, cts.Token);
        }
        catch (Exception ex) when (ex is UpstreamCredentialException or ReadOnlyViolationException or UpstreamException or ImportSetupException)
        {
            Console.Error.WriteLine($"Import stopped: {ex.Message}");
            return 1;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            Console.Error.WriteLine("Import cancelled. Re-run to continue: it is idempotent.");
            return 130;
        }
    }

    public static async Task<int> RunAsync(ImportOptions options, CancellationToken ct)
    {
        var credential = options.CookieFile is { } file
            ? UpstreamCredential.FromFile(file, options.MinimumTokenLifetime)
            : UpstreamCredential.FromEnvironment(options.MinimumTokenLifetime);
        Console.WriteLine($"Upstream: {new UriBuilder(options.UpstreamUrl) { UserName = "", Password = "" }.Uri} ({credential})");
        Console.WriteLine(options.DryRun ? "Dry run: nothing will be written to SaBai." : "Writing to SaBai.");

        // Fail before reading anything when the session is already too close to expiring.
        credential.CookieHeaderForRequest();
        using var host = ImportServices.Build(options.ConfigurationArgs);
        await EnsureDatabaseReadyAsync(host.Services, ct);
        using var upstream = new UpstreamClient(options.UpstreamUrl, credential, options.Concurrency, options.MaxAttempts);
        using var downloader = new MediaDownloader(options.UpstreamUrl, options.Concurrency, options.MaxAttempts);
        var failures = 0;

        if (options.Command is "data" or "all")
        {
            var report = new ImportReport();
            var snapshot = await UpstreamSnapshot.FetchAsync(upstream, Console.WriteLine, ct);
            Console.WriteLine(options.DryRun ? "Checking against SaBai (dry run)..." : "Writing to SaBai...");
            await new DataImporter(host.Services, report, options.DryRun, Console.WriteLine).RunAsync(snapshot, ct);
            Console.WriteLine(report.Render(options.DryRun ? "Data (dry run: would be)" : "Data"));
            failures += report.FailureCount;
        }

        if (options.Command is "media" or "all")
        {
            var report = new ImportReport();
            var embedder = new AnalyzerFaceEmbedder(host.Services.GetRequiredService<FaceAnalyzer>());
            var media = new MediaImporter(
                host.Services,
                upstream,
                downloader,
                embedder,
                report,
                new MediaOptions(
                    options.DryRun,
                    options.Concurrency,
                    options.RecordUpstreamFaceConsent,
                    !options.SkipFiles,
                    options.CloudinaryUrlPrefix
                ),
                Console.WriteLine
            );
            await media.RunAsync(ct);
            Console.WriteLine(report.Render(options.DryRun ? "Media (dry run: would be)" : "Media"));
            failures += report.FailureCount;
        }

        Console.WriteLine($"Upstream requests sent: {upstream.RequestCount} (all GET queries).");
        return failures == 0 ? 0 : 2;
    }

    /// <summary>The import writes into an existing, migrated SaBai database; it never migrates it.</summary>
    private static async Task EnsureDatabaseReadyAsync(IServiceProvider services, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SaBai.API.Data.AppDbContext>();
        if (!await db.Database.CanConnectAsync(ct))
        {
            throw new ImportSetupException("Cannot connect to the SaBai database (ConnectionStrings:sabai).");
        }

        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count > 0)
        {
            throw new ImportSetupException(
                $"The SaBai database is missing {pending.Count} migration(s). Start the SaBai API once (it migrates on start), then re-run."
            );
        }
    }
}

public sealed class ImportSetupException(string message) : Exception(message);

public sealed record ImportOptions
{
    public const string Usage =
        """
        Usage: sabai-import <data|media|all> [options] [--Config:Key=Value ...]

          data    Read every upstream record through tRPC queries and upsert it into SaBai.
          media   Store upstream patient photos (Cloudinary), enrol faces, copy patient files.
          all     data, then media.

        Options:
          --upstream-url <url>        project-sabai site, e.g. https://sabai.example.org
                                      (or SABAI_IMPORT_UPSTREAM_URL)
          --cookie-file <path>        read the upstream session cookie from this file instead of
                                      SABAI_IMPORT_UPSTREAM_COOKIE (re-read when it nears expiry)
          --min-token-minutes <n>     stop when the session has less than n minutes left (default 5, min 3)
          --concurrency <n>           requests in flight at once (default 4)
          --retries <n>               attempts per GET on network errors / 5xx / 429 (default 4)
          --dry-run                   fetch and map everything, write nothing to SaBai
          --record-upstream-face-consent
                                      media: record a biometric consent (labelled as imported) for
                                      patients upstream had enrolled in face search, then enrol them
          --skip-files                media: do not copy patient files
          --cloudinary-url-prefix <p> media: build photo URLs as <p>/<patientImagePublicId> instead of
                                      using upstream's patientImageUrl (or SABAI_IMPORT_CLOUDINARY_URL_PREFIX)
          -h, --help

        SaBai is configured like the API: ConnectionStrings__sabai, ConnectionStrings__fs-sabai,
        ConnectionStrings__qdrant and FaceRecognition__* environment variables, or the same keys as
        --ConnectionStrings:sabai=... arguments.
        """;

    public string Command { get; init; } = "";
    public Uri UpstreamUrl { get; init; } = null!;
    public string? CookieFile { get; init; }
    public TimeSpan MinimumTokenLifetime { get; init; } = TimeSpan.FromMinutes(5);
    public int Concurrency { get; init; } = 4;
    public int MaxAttempts { get; init; } = 4;
    public bool DryRun { get; init; }
    public bool RecordUpstreamFaceConsent { get; init; }
    public bool SkipFiles { get; init; }
    public string? CloudinaryUrlPrefix { get; init; }
    public bool Help { get; init; }
    public string[] ConfigurationArgs { get; init; } = [];

    public static ImportOptions Parse(string[] args)
    {
        string? command = null;
        string? url = Environment.GetEnvironmentVariable(ImportCli.UpstreamUrlVariable);
        string? cookieFile = null;
        string? prefix = Environment.GetEnvironmentVariable(ImportCli.CloudinaryPrefixVariable);
        int minutes = 5, concurrency = 4, attempts = 4;
        bool dryRun = false, consent = false, skipFiles = false, help = false;
        var config = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{arg} needs a value.");
            int NextInt(int min) =>
                int.TryParse(Next(), out var n) && n >= min ? n : throw new ArgumentException($"{arg} needs a number >= {min}.");

            switch (arg)
            {
                case "-h" or "--help":
                    help = true;
                    break;
                case "--upstream-url":
                    url = Next();
                    break;
                case "--cookie-file":
                    cookieFile = Next();
                    break;
                case "--min-token-minutes":
                    minutes = NextInt(3);
                    break;
                case "--concurrency":
                    concurrency = NextInt(1);
                    break;
                case "--retries":
                    attempts = NextInt(1);
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--record-upstream-face-consent":
                    consent = true;
                    break;
                case "--skip-files":
                    skipFiles = true;
                    break;
                case "--cloudinary-url-prefix":
                    prefix = Next();
                    break;
                case "data" or "media" or "all" when command is null:
                    command = arg;
                    break;
                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal) && arg.Contains('=') && arg.Contains(':'))
                    {
                        config.Add(arg); // .NET configuration, e.g. --ConnectionStrings:sabai=...
                        break;
                    }

                    throw new ArgumentException($"Unknown argument '{arg}'.");
            }
        }

        if (help)
        {
            return new ImportOptions { Help = true };
        }

        if (command is null)
        {
            throw new ArgumentException("Choose a command: data, media or all.");
        }

        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var upstreamUrl)
            || upstreamUrl.Scheme is not ("https" or "http"))
        {
            throw new ArgumentException($"Give the upstream site with --upstream-url or {ImportCli.UpstreamUrlVariable}.");
        }

        return new ImportOptions
        {
            Command = command,
            UpstreamUrl = upstreamUrl,
            CookieFile = cookieFile,
            MinimumTokenLifetime = TimeSpan.FromMinutes(minutes),
            Concurrency = concurrency,
            MaxAttempts = attempts,
            DryRun = dryRun,
            RecordUpstreamFaceConsent = consent,
            SkipFiles = skipFiles,
            CloudinaryUrlPrefix = string.IsNullOrWhiteSpace(prefix) ? null : prefix,
            ConfigurationArgs = [.. config],
        };
    }
}

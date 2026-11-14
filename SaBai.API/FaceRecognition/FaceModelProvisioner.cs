using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace SaBai.API.FaceRecognition;

/// <summary>
/// Makes sure the ONNX model files exist locally, downloading the model pack if allowed, and that
/// each file matches its configured SHA-256 before it is loaded.
/// </summary>
public sealed class FaceModelProvisioner
{
    private readonly FaceRecognitionOptions _options;
    private readonly ILogger<FaceModelProvisioner> _logger;
    private readonly Func<HttpClient> _createHttpClient;

    // A model that failed its hash check is not downloaded or loaded again until restart.
    private Exception? _integrityFailure;

    public FaceModelProvisioner(
        IOptions<FaceRecognitionOptions> options,
        ILogger<FaceModelProvisioner> logger
    )
        // Not using IHttpClientFactory: the default resilience pipeline times out long downloads.
        : this(options, logger, () => new HttpClient { Timeout = Timeout.InfiniteTimeSpan }) { }

    internal FaceModelProvisioner(
        IOptions<FaceRecognitionOptions> options,
        ILogger<FaceModelProvisioner> logger,
        Func<HttpClient> createHttpClient
    )
    {
        _options = options.Value;
        _logger = logger;
        _createHttpClient = createHttpClient;
    }

    public string DetectorPath => Path.Combine(_options.ModelsDirectory, _options.DetectorModel);
    public string RecognizerPath =>
        Path.Combine(_options.ModelsDirectory, _options.RecognizerModel);

    public async Task EnsureModelsAsync(CancellationToken ct)
    {
        if (_integrityFailure is not null)
            throw _integrityFailure;

        (string Path, string Sha256, string Setting)[] models =
        [
            (DetectorPath, _options.DetectorModelSha256, nameof(_options.DetectorModelSha256)),
            (
                RecognizerPath,
                _options.RecognizerModelSha256,
                nameof(_options.RecognizerModelSha256)
            ),
        ];

        if (_options.AutoDownloadModels)
        {
            var unpinned = models.Where(m => string.IsNullOrWhiteSpace(m.Sha256)).ToList();
            if (unpinned.Count > 0)
                throw new InvalidOperationException(
                    $"{FaceRecognitionOptions.SectionName}:AutoDownloadModels requires the SHA-256 of every model. "
                        + $"Set {string.Join(" and ", unpinned.Select(m => $"{FaceRecognitionOptions.SectionName}:{m.Setting}"))}, "
                        + "or provide the files yourself and turn AutoDownloadModels off."
                );
        }

        foreach (var model in models.Where(m => File.Exists(m.Path)))
            await VerifyAsync(model.Path, model.Path, model.Sha256, model.Setting, ct);

        var missing = models.Where(m => !File.Exists(m.Path)).ToList();
        if (missing.Count == 0)
            return;

        if (!_options.AutoDownloadModels)
            throw new FileNotFoundException(
                $"Face recognition model(s) not found: {string.Join(", ", missing.Select(m => m.Path))}. "
                    + $"Place them in '{_options.ModelsDirectory}' or enable {FaceRecognitionOptions.SectionName}:AutoDownloadModels."
            );

        Directory.CreateDirectory(_options.ModelsDirectory);
        var archive = Path.Combine(_options.ModelsDirectory, $"download-{Guid.NewGuid():N}.zip");
        try
        {
            _logger.LogInformation(
                "Downloading face recognition models from {Url}",
                _options.ModelPackUrl
            );
            using (var http = _createHttpClient())
            using (
                var response = await http.GetAsync(
                    _options.ModelPackUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    ct
                )
            )
            {
                response.EnsureSuccessStatusCode();
                await using var file = File.Create(archive);
                await response.Content.CopyToAsync(file, ct);
            }

            using var zip = ZipFile.OpenRead(archive);
            foreach (var model in missing)
            {
                var name = Path.GetFileName(model.Path);
                var entry =
                    zip.Entries.FirstOrDefault(e =>
                        e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                    )
                    ?? throw new FileNotFoundException(
                        $"Model '{name}' not found in {_options.ModelPackUrl}."
                    );

                var temp = model.Path + ".tmp";
                entry.ExtractToFile(temp, overwrite: true);
                await VerifyAsync(temp, model.Path, model.Sha256, model.Setting, ct);
                File.Move(temp, model.Path, overwrite: true);
                _logger.LogInformation("Extracted face model {Model}", model.Path);
            }
        }
        finally
        {
            File.Delete(archive);
        }
    }

    /// <summary>
    /// Checks <paramref name="file"/> against the expected hash. On a mismatch the file is deleted
    /// and face recognition stays unavailable until the process restarts.
    /// </summary>
    private async Task VerifyAsync(
        string file,
        string model,
        string expectedSha256,
        string setting,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(expectedSha256))
            return;

        string actual;
        await using (var stream = File.OpenRead(file))
            actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));

        if (actual.Equals(expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
            return;

        File.Delete(file);
        _integrityFailure = new InvalidDataException(
            $"Face recognition model '{model}' has SHA-256 {actual}, but {FaceRecognitionOptions.SectionName}:{setting} "
                + $"expects {expectedSha256}. The file was deleted and face recognition is disabled until restart."
        );
        _logger.LogError(
            "Face recognition model {Model} failed its SHA-256 check and was deleted",
            model
        );
        throw _integrityFailure;
    }

    internal static bool IsValidSha256(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || (value.Trim().Length == 64 && value.Trim().All(char.IsAsciiHexDigit));
}

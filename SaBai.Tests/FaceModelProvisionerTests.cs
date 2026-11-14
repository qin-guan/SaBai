using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using SaBai.API.FaceRecognition;

namespace SaBai.Tests;

/// <summary>Model files are only used when they match their pinned SHA-256.</summary>
public sealed class FaceModelProvisionerTests
{
    private static readonly byte[] Detector = [1, 2, 3, 4];
    private static readonly byte[] Recognizer = [5, 6, 7, 8, 9];

    [Test]
    public async Task Download_WithMatchingHashes_ExtractsModels()
    {
        using var dir = new TempDirectory();
        var (provisioner, downloads) = Create(dir.Path, Pack(Detector, Recognizer), Options(dir.Path));

        await provisioner.EnsureModelsAsync(CancellationToken.None);

        await Assert.That(File.ReadAllBytes(provisioner.DetectorPath).SequenceEqual(Detector)).IsTrue();
        await Assert
            .That(File.ReadAllBytes(provisioner.RecognizerPath).SequenceEqual(Recognizer))
            .IsTrue();
        await Assert.That(downloads.Count).IsEqualTo(1);
        await Assert.That(Directory.GetFiles(dir.Path).Length).IsEqualTo(2);
    }

    [Test]
    public async Task Download_WithTamperedModel_DeletesItAndRefuses()
    {
        using var dir = new TempDirectory();
        var (provisioner, downloads) = Create(
            dir.Path,
            Pack(Detector, [6, 6, 6]),
            Options(dir.Path)
        );

        var first = await Assert.ThrowsAsync<InvalidDataException>(() =>
            provisioner.EnsureModelsAsync(CancellationToken.None)
        );
        // Refused again without downloading until the process restarts.
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            provisioner.EnsureModelsAsync(CancellationToken.None)
        );

        await Assert.That(first!.Message).Contains("RecognizerModelSha256");
        await Assert.That(File.Exists(provisioner.RecognizerPath)).IsFalse();
        await Assert.That(Directory.GetFiles(dir.Path, "*.tmp").Length).IsEqualTo(0);
        await Assert.That(downloads.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ExistingFile_WithWrongHash_IsDeletedAndNotUsed()
    {
        using var dir = new TempDirectory();
        var options = Options(dir.Path);
        options.AutoDownloadModels = false;
        File.WriteAllBytes(Path.Combine(dir.Path, options.DetectorModel), [9, 9, 9]);
        File.WriteAllBytes(Path.Combine(dir.Path, options.RecognizerModel), Recognizer);
        var (provisioner, downloads) = Create(dir.Path, Pack(Detector, Recognizer), options);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            provisioner.EnsureModelsAsync(CancellationToken.None)
        );

        await Assert.That(error!.Message).Contains("DetectorModelSha256");
        await Assert.That(File.Exists(provisioner.DetectorPath)).IsFalse();
        await Assert.That(downloads.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AutoDownload_WithoutPinnedHash_Refuses()
    {
        using var dir = new TempDirectory();
        var options = Options(dir.Path);
        options.RecognizerModelSha256 = "";
        var (provisioner, downloads) = Create(dir.Path, Pack(Detector, Recognizer), options);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provisioner.EnsureModelsAsync(CancellationToken.None)
        );

        await Assert.That(error!.Message).Contains("RecognizerModelSha256");
        await Assert.That(downloads.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Defaults_PinTheBuffaloLModels()
    {
        var options = new FaceRecognitionOptions();

        await Assert
            .That(options.DetectorModelSha256)
            .IsEqualTo("5838f7fe053675b1c7a08b633df49e7af5495cee0493c7dcf6697200b85b5b91");
        await Assert
            .That(options.RecognizerModelSha256)
            .IsEqualTo("4c06341c33c2ca1f86781dab0e829f88ad5b64be9fba56e56bc9ebdefc619e43");
    }

    private static FaceRecognitionOptions Options(string dir) =>
        new()
        {
            ModelsDirectory = dir,
            ModelPackUrl = "https://models.invalid/pack.zip",
            DetectorModelSha256 = Sha256(Detector),
            RecognizerModelSha256 = Sha256(Recognizer),
        };

    private static (FaceModelProvisioner, List<Uri>) Create(
        string dir,
        byte[] pack,
        FaceRecognitionOptions options
    )
    {
        var downloads = new List<Uri>();
        var provisioner = new FaceModelProvisioner(
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<FaceModelProvisioner>.Instance,
            () => new HttpClient(new PackHandler(pack, downloads))
        );
        return (provisioner, downloads);
    }

    private static byte[] Pack(byte[] detector, byte[] recognizer)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "buffalo_l/det_10g.onnx", detector);
            Add(zip, "buffalo_l/w600k_r50.onnx", recognizer);
        }

        return buffer.ToArray();
    }

    private static void Add(ZipArchive zip, string name, byte[] bytes)
    {
        using var entry = zip.CreateEntry(name).Open();
        entry.Write(bytes);
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed class PackHandler(byte[] pack, List<Uri> downloads) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            downloads.Add(request.RequestUri!);
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(pack) }
            );
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } =
            Directory.CreateTempSubdirectory("sabai-models-").FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}

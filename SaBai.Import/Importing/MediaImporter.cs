using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaBai.API.Data;
using SaBai.API.Domain;
using SaBai.API.Endpoints.Faces;
using SaBai.API.Endpoints.Patients;
using SaBai.API.FaceRecognition;
using SaBai.API.Storage;
using SaBai.Import.Upstream;
using Ids = SaBai.Import.Importing.DeterministicIds;

namespace SaBai.Import.Importing;

/// <summary>Turns a photo into the face embedding to enrol (the most prominent face), or null.</summary>
public interface IFaceEmbedder
{
    Task<float[]?> EmbedLargestFaceAsync(RgbImage image, CancellationToken ct);
}

/// <summary>The API's own pipeline: <see cref="FaceAnalyzer"/> and <see cref="FaceImages.Largest"/>.</summary>
public sealed class AnalyzerFaceEmbedder(FaceAnalyzer analyzer) : IFaceEmbedder
{
    public async Task<float[]?> EmbedLargestFaceAsync(RgbImage image, CancellationToken ct) =>
        FaceImages.Largest(await analyzer.AnalyzeAsync(image, ct))?.Embedding;
}

public sealed record MediaOptions(
    bool DryRun,
    int Concurrency,
    bool RecordUpstreamFaceConsent,
    bool IncludeFiles,
    string? CloudinaryUrlPrefix
);

/// <summary>
/// Phase 2: for patients the data phase imported, stores the upstream photo (downloaded from
/// Cloudinary) the way <c>PUT /patients/{id}/photo</c> does, enrols the face the way
/// <c>POST /patients/{id}/faces</c> does (biometric consent required), and copies the patient's
/// files from upstream's S3 the way <c>POST /files</c> stores them. Each step is skipped when it
/// is already done, so the phase can be re-run to resume; a failure is reported and the rest
/// continue.
/// </summary>
public sealed class MediaImporter(
    IServiceProvider services,
    UpstreamClient upstream,
    MediaDownloader downloader,
    IFaceEmbedder embedder,
    ImportReport report,
    MediaOptions options,
    Action<string> log
)
{
    public const string Photos = "patient photos";
    public const string Faces = "patient faces";
    public const string Files = "patient files";

    private const long MaxPixels = 50_000_000;

    private int _noConsent;
    private int _notImported;
    private Guid _importUser;

    public async Task RunAsync(CancellationToken ct)
    {
        await using (var scope = services.CreateAsyncScope())
        {
            _importUser = await ImportActor.EnsureUserAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), options.DryRun, ct);
        }

        using var actor = ImportActor.Begin("media", _importUser);
        log("Fetching upstream patients (read-only query)...");
        var patients = await upstream.QueryAsync<List<UpPatient>>(UpstreamClient.Procedures.PatientsList, null, ct);
        log($"  {patients.Count} patients; downloading photos{(options.IncludeFiles ? " and files" : "")}...");

        var done = 0;
        await Parallel.ForEachAsync(
            patients,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, options.Concurrency), CancellationToken = ct },
            async (patient, token) =>
            {
                await ProcessAsync(patient, token);
                if (Interlocked.Increment(ref done) % 50 == 0)
                {
                    log($"  {done}/{patients.Count} patients");
                }
            }
        );

        if (_notImported > 0)
        {
            report.Note($"{_notImported} patient(s) are not in SaBai yet, so their media was not checked (dry run)");
        }

        if (_noConsent > 0)
        {
            report.Note(
                $"{_noConsent} face(s) not enrolled: the patient has no biometric consent here "
                    + "(record it in SaBai, or re-run media with --record-upstream-face-consent; see README)"
            );
        }
    }

    private async Task ProcessAsync(UpPatient up, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var patient = await db.Patients.FirstOrDefaultAsync(x => x.Id == Ids.Patient(up.Id), ct);
        var url = PhotoUrl(up);
        if (url is not null)
        {
            report.Fetched(Photos, 1);
            report.Fetched(Faces, 1);
        }

        if (patient is null)
        {
            if (options.DryRun)
            {
                // Expected in a dry run of "all": the data phase wrote nothing.
                Interlocked.Increment(ref _notImported);
                if (url is not null)
                {
                    report.Record(Photos, Outcome.Skipped);
                    report.Record(Faces, Outcome.Skipped);
                }
            }
            else if (url is not null)
            {
                report.Fail(Photos, $"patient {up.Id}", "patient is not in SaBai (run the data phase first)");
                report.Record(Faces, Outcome.Skipped);
            }

            return;
        }

        if (url is not null)
        {
            var photo = await PhotoAsync(db, scope.ServiceProvider, up, patient, url, ct);
            await FaceAsync(db, scope.ServiceProvider, up, patient, url, photo, ct);
        }

        if (options.IncludeFiles)
        {
            await FilesAsync(db, scope.ServiceProvider, up, patient, ct);
        }
    }

    private Uri? PhotoUrl(UpPatient up)
    {
        if (!string.IsNullOrWhiteSpace(options.CloudinaryUrlPrefix) && !string.IsNullOrWhiteSpace(up.PatientImagePublicId))
        {
            return new Uri($"{options.CloudinaryUrlPrefix.TrimEnd('/')}/{up.PatientImagePublicId.TrimStart('/')}");
        }

        return Uri.TryCreate(up.PatientImageUrl, UriKind.Absolute, out var uri) ? uri : null;
    }

    /// <summary>The validated photo bytes when they were downloaded in this run.</summary>
    private async Task<(byte[] Bytes, RgbImage Image)?> PhotoAsync(
        AppDbContext db,
        IServiceProvider sp,
        UpPatient up,
        Patient patient,
        Uri url,
        CancellationToken ct
    )
    {
        if (patient.PhotoStoredFileName is not null)
        {
            report.Record(Photos, Outcome.Skipped);
            return null;
        }

        try
        {
            var (bytes, contentType, image) = await DownloadImageAsync(url, ct);
            if (!options.DryRun)
            {
                var key = $"patients/{patient.Id}/{Ids.PatientPhoto(up.Id):N}{FileSignatures.ExtensionFor(contentType)}";
                await PutAsync(sp, key, bytes, contentType, ct);
                patient.PhotoStoredFileName = key;
                await db.SaveChangesAsync(ct);
            }

            report.Record(Photos, Outcome.Created);
            return (bytes, image);
        }
        catch (Exception ex) when (IsItemError(ex, ct))
        {
            report.Fail(Photos, $"patient {up.Id}", ex.Message);
            return null;
        }
    }

    private async Task FaceAsync(
        AppDbContext db,
        IServiceProvider sp,
        UpPatient up,
        Patient patient,
        Uri url,
        (byte[] Bytes, RgbImage Image)? photo,
        CancellationToken ct
    )
    {
        if (patient.LatestFaceId is not null)
        {
            report.Record(Faces, Outcome.Skipped);
            return;
        }

        try
        {
            var consent = await ConsentAsync(db, up, patient, ct);
            if (consent == Consent.None)
            {
                Interlocked.Increment(ref _noConsent);
                report.Record(Faces, Outcome.Skipped);
                return;
            }

            if (options.DryRun)
            {
                report.Record(Faces, Outcome.Created);
                return;
            }

            var store = sp.GetRequiredService<FaceStore>();
            var personId = PatientFaceIdentity.ForPatient(patient.Id);
            // A face stored by an earlier run that stopped before saving the patient: use it.
            var existing = consent == Consent.Granted ? await store.ListAsync(personId, ct) : [];
            if (existing.Count > 0)
            {
                patient.LatestFaceId = existing[^1].FaceId.ToString();
                await db.SaveChangesAsync(ct);
                report.Record(Faces, Outcome.Updated);
                return;
            }

            var image = photo?.Image ?? (await DownloadImageAsync(url, ct)).Image;
            var embedding = await embedder.EmbedLargestFaceAsync(image, ct)
                ?? throw new MediaException("No usable face detected in the upstream photo.");
            if (consent == Consent.ToRecord)
            {
                await RecordConsentAsync(db, up, patient, ct);
            }

            var stored = await store.AddAsync(personId, embedding, ct);
            // As the API does: consent withdrawn meanwhile removes the face just stored.
            if (!await BiometricConsent.IsGrantedAsync(db, patient.Id, ct))
            {
                await store.DeleteFaceAsync(stored.FaceId, ct);
                throw new MediaException("biometric consent was withdrawn during the import");
            }

            patient.LatestFaceId = stored.FaceId.ToString();
            await db.SaveChangesAsync(ct);
            report.Record(Faces, Outcome.Created);
        }
        catch (Exception ex) when (IsItemError(ex, ct))
        {
            report.Fail(Faces, $"patient {up.Id}", ex.Message);
        }
    }

    private enum Consent
    {
        None,
        Granted,
        ToRecord,
    }

    /// <summary>
    /// Faces are only enrolled with current biometric consent, as through the API. Upstream has
    /// no consent records. With <c>--record-upstream-face-consent</c>, a consent is recorded for
    /// patients upstream had enrolled in face search (it has a Rekognition face id), attributed
    /// to the import account and labelled as such, once a usable face was found. A consent
    /// recorded by an earlier run and revoked since is respected.
    /// </summary>
    private async Task<Consent> ConsentAsync(AppDbContext db, UpPatient up, Patient patient, CancellationToken ct)
    {
        if (await BiometricConsent.IsGrantedAsync(db, patient.Id, ct))
        {
            return Consent.Granted;
        }

        if (
            !options.RecordUpstreamFaceConsent
            || string.IsNullOrWhiteSpace(up.RekognitionFaceId)
            || await db.PatientConsents.AnyAsync(x => x.Id == Ids.FaceConsent(up.Id), ct)
        )
        {
            return Consent.None;
        }

        return Consent.ToRecord;
    }

    private async Task RecordConsentAsync(AppDbContext db, UpPatient up, Patient patient, CancellationToken ct)
    {
        db.PatientConsents.Add(
            new PatientConsent
            {
                Id = Ids.FaceConsent(up.Id),
                PatientId = patient.Id,
                ConsentType = ConsentType.BiometricFaceRecognition,
                Granted = true,
                RecordedAt = DateTimeOffset.UtcNow,
                RecordedByUserId = _importUser,
                Notes =
                    "Recorded by the upstream import (--record-upstream-face-consent): the patient's face was "
                    + "enrolled in face search in project-sabai. Not a consent given in SaBai.",
            }
        );
        await db.SaveChangesAsync(ct);
    }

    private async Task FilesAsync(AppDbContext db, IServiceProvider sp, UpPatient up, Patient patient, CancellationToken ct)
    {
        List<UpFile> files;
        try
        {
            files = await upstream.QueryAsync<List<UpFile>?>(
                UpstreamClient.Procedures.FilesByPatient,
                new { patientId = up.Id },
                ct
            ) ?? [];
        }
        catch (UpstreamException ex)
        {
            report.Fail(Files, $"list for patient {up.Id}", ex.Message);
            return;
        }

        report.Fetched(Files, files.Count);
        foreach (var file in files)
        {
            var id = Ids.File(file.Id);
            try
            {
                if (await db.Files.AnyAsync(x => x.Id == id, ct))
                {
                    report.Record(Files, Outcome.Skipped);
                    continue;
                }

                var name = Path.GetFileName(file.FileName.Trim());
                if (string.IsNullOrWhiteSpace(name) || name.Length > 255)
                {
                    throw new MediaException("file name is empty or longer than 255 characters");
                }

                if (!Uri.TryCreate(file.FileUrl, UriKind.Absolute, out var url))
                {
                    throw new MediaException("upstream gave no usable download link");
                }

                var bytes = await downloader.GetAsync(url, StoredFile.MaxBytes, ct);
                if (bytes.Length == 0)
                {
                    throw new MediaException("file is empty");
                }

                // The type comes from the bytes, as in POST /files.
                var contentType = await FileSignatures.DetectAsync(new MemoryStream(bytes), ct);
                if (contentType is null || !StoredFile.AllowedContentTypes.Contains(contentType))
                {
                    throw new MediaException("only PDF, JPEG, PNG and DOCX files can be stored");
                }

                if (!options.DryRun)
                {
                    var storedName = $"{id:N}{FileSignatures.ExtensionFor(contentType)}";
                    await PutAsync(sp, storedName, bytes, contentType, ct);
                    db.Files.Add(
                        new StoredFile
                        {
                            Id = id,
                            PatientId = patient.Id,
                            FileName = name,
                            StoredFileName = storedName,
                            Description = file.Description ?? "",
                            SizeBytes = bytes.Length,
                            IsDeleted = false,
                            CreatedAt = file.CreatedAt,
                        }
                    );
                    await db.SaveChangesAsync(ct);
                }

                report.Record(Files, Outcome.Created);
            }
            catch (Exception ex) when (IsItemError(ex, ct))
            {
                db.ChangeTracker.Clear();
                report.Fail(Files, $"{file.Id} (patient {up.Id})", ex.Message);
            }
        }
    }

    /// <summary>Downloads and checks a photo as <c>PatientPhoto.ReadValidatedAsync</c> does.</summary>
    private async Task<(byte[] Bytes, string ContentType, RgbImage Image)> DownloadImageAsync(Uri url, CancellationToken ct)
    {
        var bytes = await downloader.GetAsync(url, PatientPhoto.MaxBytes, ct);
        var contentType = await FileSignatures.DetectAsync(new MemoryStream(bytes), ct);
        if (contentType is null || !FileSignatures.IsImage(contentType))
        {
            throw new MediaException("the photo is not a JPEG or PNG image");
        }

        var image = RgbImage.Decode(new MemoryStream(bytes));
        if ((long)image.Width * image.Height > MaxPixels)
        {
            throw new MediaException("image resolution is too large");
        }

        return (bytes, contentType, image);
    }

    private static async Task PutAsync(IServiceProvider sp, string key, byte[] bytes, string contentType, CancellationToken ct)
    {
        var s3 = sp.GetRequiredService<IAmazonS3>();
        var storage = sp.GetRequiredService<RustFsSettings>();
        await using var stream = new MemoryStream(bytes);
        await s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = storage.Bucket,
                Key = key,
                InputStream = stream,
                ContentType = contentType,
                AutoCloseStream = false,
            },
            ct
        );
    }

    /// <summary>Per-item problems are reported; a read-only violation or cancellation stops the run.</summary>
    private static bool IsItemError(Exception ex, CancellationToken ct) =>
        ex is not ReadOnlyViolationException and not UpstreamCredentialException
        && !(ex is OperationCanceledException && ct.IsCancellationRequested);
}

public sealed class MediaException(string message) : Exception(message);

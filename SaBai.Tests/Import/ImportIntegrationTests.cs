using System.Data.Common;
using System.Text.Json.Nodes;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using SaBai.API.Data;
using SaBai.API.Domain;
using SaBai.API.FaceRecognition;
using SaBai.API.Storage;
using SaBai.Import.Importing;
using SaBai.Import.Upstream;
using SaBai.Tests.Support;
using Ids = SaBai.Import.Importing.DeterministicIds;

namespace SaBai.Tests.Import;

/// <summary>
/// Runs the import against a fake upstream (tRPC) and a fake image/file host into the real test
/// stack (Postgres, RustFS, Qdrant). The test database is shared and may outlive a test session,
/// so assertions hold whether the fixture rows are new or left by an earlier session.
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel(nameof(ImportIntegrationTests))]
public sealed class ImportIntegrationTests(AppFixture fixture)
{
    private static readonly string[] Entities =
    [
        "staff users", "village codes", "patients", "visits", "vitals", "puberty", "eyesight", "consults",
        "diagnoses", "referral partners", "referrals", "ingredients", "brands", "stock", "broadcasts",
    ];

    [Test]
    public async Task Import_BackfillsDataAndMedia_ThenASecondRunChangesNothing()
    {
        var ct = CancellationToken.None;
        await using var media = await FakeMediaHost.StartAsync();
        var dataset = FakeUpstream.LoadDataset(media.BaseUrl);
        var taken = await AddPatientWhoseNumberIsTakenAsync(dataset);
        await using var upstream = await FakeUpstream.StartAsync(dataset);
        using var host = await BuildServicesAsync();

        var first = await RunAsync(host, upstream, dryRun: false, ct);
        var countsAfterFirst = await CountImportedRowsAsync(host, dataset);

        // Every row is there and nothing failed except the photo upstream no longer has.
        foreach (var entity in Entities)
        {
            await Assert.That(first.Data[entity].Failed).IsEqualTo(0);
        }

        await Assert.That(first.Media.Failures.Select(x => (x.Entity, x.Key)).ToArray()).IsEquivalentTo(new[]
        {
            (MediaImporter.Photos, "patient 710003"),
            (MediaImporter.Faces, "patient 710003"),
        });
        await Assert.That(first.Data["stock"].Fetched).IsEqualTo(4);

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await AssertDataAsync(db, taken);
            await AssertMediaAsync(db, scope.ServiceProvider);
        }

        // Only GET queries on the allowlist reached upstream, all with the operator's cookie.
        await Assert.That(upstream.Requests.All(x => x.Method == "GET")).IsTrue();
        await Assert.That(upstream.Requests.Select(x => x.Path["/api/trpc/".Length..]).Distinct()
            .All(UpstreamClient.AllowedQueries.Contains)).IsTrue();
        await Assert.That(upstream.Requests.All(x => x.Cookie!.Contains("sb-fake-auth-token"))).IsTrue();
        await Assert.That(media.Requests.All(x => x.Method == "GET" && string.IsNullOrEmpty(x.Cookie))).IsTrue();

        // Second run: same rows, nothing created or changed, no new failures.
        var second = await RunAsync(host, upstream, dryRun: false, ct);
        foreach (var entity in Entities)
        {
            var c = second.Data[entity];
            await Assert.That(c.Created + c.Updated + c.Failed).IsEqualTo(0);
        }

        await Assert.That(second.Media[MediaImporter.Photos].Created).IsEqualTo(0);
        await Assert.That(second.Media[MediaImporter.Faces].Created).IsEqualTo(0);
        await Assert.That(second.Media[MediaImporter.Files].Created).IsEqualTo(0);
        await Assert.That(await CountImportedRowsAsync(host, dataset)).IsEquivalentTo(countsAfterFirst);
    }

    [Test]
    public async Task DryRun_FetchesAndMapsButWritesNothing()
    {
        var ct = CancellationToken.None;
        await using var media = await FakeMediaHost.StartAsync();
        // Fresh upstream ids so the rows cannot exist from another run.
        var offset = Random.Shared.Next(1_000, 900_000) * 10;
        var dataset = new JsonObject
        {
            ["villageCodesRouter.list"] = new JsonArray(new JsonObject
            {
                ["id"] = offset, ["code"] = $"DRY{offset}", ["name"] = "Dry", ["colorHex"] = "#000000",
                ["isVisible"] = true, ["createdAt"] = "2025-01-01T00:00:00Z", ["updatedAt"] = "2025-01-01T00:00:00Z",
            }),
            ["patientsRouter.list"] = new JsonArray(new JsonObject
            {
                ["id"] = offset + 1, ["name"] = "Dry Run", ["gender"] = "male", ["dateOfBirth"] = "2000-01-01T00:00:00Z",
                ["hasPoorCard"] = false, ["hasBS2Card"] = false, ["drugAllergy"] = "", ["hasSabaiCard"] = false,
                ["patientImagePublicId"] = "image/upload/v1700000002/p710002", ["rekognitionFaceId"] = null,
                ["patientImageUrl"] = $"{media.BaseUrl.ToString().TrimEnd('/')}/demo/image/upload/v1700000002/p710002",
            }),
            ["visitsRouter.list"] = new JsonArray(new JsonObject
            {
                ["id"] = offset + 2, ["date"] = "2025-03-03T00:00:00Z", ["patientId"] = offset + 1, ["villageCodeId"] = offset,
            }),
            ["vitalsRouter.getByVisitId"] = new JsonObject(),
            ["pubertyRouter.getByVitalId"] = new JsonObject(),
            ["eyesightRouter.getByVisitId"] = new JsonObject(),
            ["consultsRouter.getByVisitId"] = new JsonObject { [$"{offset + 2}"] = new JsonArray() },
            ["referralRouter.list"] = new JsonArray(),
            ["referralPartnersRouter.list"] = new JsonArray(),
            ["medicationActiveIngredientsRouter.list"] = new JsonArray(),
            ["medicationBrandRouter.list"] = new JsonArray(),
            ["medicationStockRouter.list"] = new JsonArray(),
            ["broadcastsRouter.list"] = new JsonArray(),
            ["filesRouter.listByPatientId"] = new JsonObject(),
        };
        await using var upstream = await FakeUpstream.StartAsync(dataset);
        using var host = await BuildServicesAsync();

        var result = await RunAsync(host, upstream, dryRun: true, ct);

        await Assert.That(result.Data["patients"].Created).IsEqualTo(1);
        await Assert.That(result.Data["visits"].Created).IsEqualTo(1);
        // The patient is not in SaBai (nothing was written), so its media is skipped, not failed.
        await Assert.That(result.Media[MediaImporter.Photos].Skipped).IsEqualTo(1);
        await Assert.That(result.Media.FailureCount).IsEqualTo(0);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await Assert.That(await db.Patients.AnyAsync(x => x.Id == Ids.Patient(offset + 1), ct)).IsFalse();
        await Assert.That(await db.VillageCodes.AnyAsync(x => x.Code == $"DRY{offset}", ct)).IsFalse();
        await Assert.That(await db.Visits.AnyAsync(x => x.Id == Ids.Visit(offset + 2), ct)).IsFalse();
    }

    private sealed record Result(ImportReport Data, ImportReport Media);

    private static async Task<Result> RunAsync(IHost host, FakeUpstream upstream, bool dryRun, CancellationToken ct)
    {
        var credential = UpstreamCredential.FromValue(FakeUpstream.Cookie(TimeSpan.FromHours(1)), TimeSpan.FromMinutes(5));
        using var client = new UpstreamClient(upstream.BaseUrl, credential, concurrency: 4);
        using var downloader = new MediaDownloader(upstream.BaseUrl, maxAttempts: 1);
        var data = new ImportReport();
        var snapshot = await UpstreamSnapshot.FetchAsync(client, _ => { }, ct);
        await new DataImporter(host.Services, data, dryRun, _ => { }).RunAsync(snapshot, ct);
        var media = new ImportReport();
        await new MediaImporter(
            host.Services,
            client,
            downloader,
            new FixedEmbedder(),
            media,
            new MediaOptions(dryRun, 2, RecordUpstreamFaceConsent: true, IncludeFiles: true, CloudinaryUrlPrefix: null),
            _ => { }
        ).RunAsync(ct);
        return new Result(data, media);
    }

    /// <summary>
    /// Registers a patient through the API and adds an upstream patient whose id equals its
    /// number, so the import has to give that one a new number.
    /// </summary>
    private async Task<(int UpstreamId, Guid ExistingPatient)> AddPatientWhoseNumberIsTakenAsync(JsonObject dataset)
    {
        var client = await fixture.CreateApiClient().LoginAsync();
        var village = await ClinicSetup.CreateVillageAsync(client);
        var (patientId, _) = await ClinicSetup.RegisterPatientAsync(client, village);
        using var host = await BuildServicesAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var number = await db.Patients.Where(x => x.Id == patientId).Select(x => x.Number).SingleAsync();
        dataset["patientsRouter.list"]!.AsArray().Add(new JsonObject
        {
            ["id"] = number, ["name"] = "Number Clash", ["identificationNumber"] = null, ["contactNo"] = null,
            ["gender"] = "male", ["dateOfBirth"] = "1980-02-02T00:00:00Z", ["hasPoorCard"] = false,
            ["hasBS2Card"] = false, ["drugAllergy"] = "", ["hasSabaiCard"] = false,
            ["patientImagePublicId"] = null, ["rekognitionFaceId"] = null, ["patientImageUrl"] = null,
        });
        return (number, patientId);
    }

    private static async Task AssertDataAsync(AppDbContext db, (int UpstreamId, Guid ExistingPatient) taken)
    {
        var importUser = await ImportUserIdAsync(db);
        var dara = await db.Patients.Include(x => x.VillageCode).SingleAsync(x => x.Id == Ids.Patient(710001));
        await Assert.That(dara.Number).IsEqualTo(710001);
        await Assert.That(dara.Name).IsEqualTo("Sok Dara");
        // Latest visit's village (720002 at UPV2), as upstream shows it.
        await Assert.That(dara.VillageCode!.Code).IsEqualTo("UPV2");
        await Assert.That(dara.CreatedAt).IsEqualTo(DateTimeOffset.Parse("2025-06-01T02:00:00Z"));
        await Assert.That(dara.UpdatedByUserId).IsEqualTo(importUser);

        var clash = await db.Patients.SingleAsync(x => x.Id == Ids.Patient(taken.UpstreamId));
        await Assert.That(clash.Number).IsNotEqualTo(taken.UpstreamId);
        await Assert.That(await db.Patients.Where(x => x.Id == taken.ExistingPatient).Select(x => x.Number).SingleAsync())
            .IsEqualTo(taken.UpstreamId);

        var hidden = await db.VillageCodes.SingleAsync(x => x.Id == Ids.VillageCode(71002));
        await Assert.That(hidden.IsVisible).IsFalse();

        var vitals = await db.Vitals.Include(x => x.Puberty).SingleAsync(x => x.VisitId == Ids.Visit(720001));
        await Assert.That(vitals.Height).IsEqualTo(120.50m);
        await Assert.That(vitals.Puberty!.PubarcheAge).IsEqualTo(11);
        await Assert.That(await db.Eyesights.AnyAsync(x => x.VisitId == Ids.Visit(720001))).IsTrue();

        var consult = await db.Consults.Include(x => x.Diagnoses).Include(x => x.Doctor).SingleAsync(x => x.Id == Ids.Consult(760001));
        await Assert.That(consult.Diagnoses.Count).IsEqualTo(3);
        // Named after the broadcast sender; cannot sign in.
        await Assert.That(consult.Doctor.Nickname).IsEqualTo("Dr Alice");
        await Assert.That(consult.Doctor.PasswordHash).IsNull();
        await Assert.That(consult.Doctor.LockoutEnd).IsEqualTo(DateTimeOffset.MaxValue);

        var referral = await db.Referrals.SingleAsync(x => x.Id == Ids.Referral(780001));
        await Assert.That(referral.ReferralState).IsEqualTo(ReferralState.Seen);

        var stock = await db.MedicationStocks.Where(x => x.MedicationBrandId == Ids.Brand(790101)).ToListAsync();
        await Assert.That(stock.Select(x => x.Id).Order().ToArray())
            .IsEquivalentTo(new[] { Ids.Stock(790201), Ids.Stock(790202), Ids.Stock(790204) }.Order().ToArray());
        await Assert.That(stock.All(x => x.ReservedQuantity == 0 && x.ReservedQuantity <= x.Quantity)).IsTrue();
        var reserved = stock.Single(x => x.Id == Ids.Stock(790202));
        await Assert.That(reserved.StockStatus).IsEqualTo(StockStatus.Active);
        await Assert.That(reserved.Quantity).IsEqualTo(30);
        // Same creation log rows the API writes, by the import account.
        await Assert.That(await db.StockChanges.CountAsync(x => x.StockId == Ids.Stock(790201) && x.UserId == importUser))
            .IsGreaterThanOrEqualTo(3);

        // Rows written by the import are in the audit log, attributed to the import account.
        await Assert.That(await db.AuditLogs.AnyAsync(x =>
            x.EntityType == nameof(Patient) && x.EntityId == Ids.Patient(710001).ToString() && x.UserName == ImportActor.UserName
        )).IsTrue();
    }

    private static async Task AssertMediaAsync(AppDbContext db, IServiceProvider services)
    {
        var importUser = await ImportUserIdAsync(db);
        var dara = await db.Patients.SingleAsync(x => x.Id == Ids.Patient(710001));
        var vuthy = await db.Patients.SingleAsync(x => x.Id == Ids.Patient(710002));
        await Assert.That(dara.PhotoStoredFileName).IsEqualTo($"patients/{dara.Id}/{Ids.PatientPhoto(710001):N}.png");
        await Assert.That(vuthy.PhotoStoredFileName).IsEqualTo($"patients/{vuthy.Id}/{Ids.PatientPhoto(710002):N}.jpg");

        var s3 = services.GetRequiredService<IAmazonS3>();
        var bucket = services.GetRequiredService<RustFsSettings>().Bucket;
        var photo = await s3.GetObjectMetadataAsync(bucket, dara.PhotoStoredFileName);
        await Assert.That(photo.Headers.ContentType).IsEqualTo("image/png");

        // Upstream had enrolled Dara's face: consent recorded (labelled) and face enrolled in Qdrant.
        // Vuthy had no upstream face and no consent here: photo only.
        await Assert.That(dara.LatestFaceId).IsNotNull();
        await Assert.That(await db.PatientConsents.AnyAsync(x =>
            x.Id == Ids.FaceConsent(710001) && x.RecordedByUserId == importUser && x.Granted)).IsTrue();
        var faces = await services.GetRequiredService<FaceStore>().ListAsync($"patient:{dara.Id}", CancellationToken.None);
        await Assert.That(faces.Select(x => x.FaceId.ToString())).Contains(dara.LatestFaceId!);
        await Assert.That(vuthy.LatestFaceId).IsNull();

        var file = await db.Files.SingleAsync(x => x.Id == Ids.File(790401));
        await Assert.That(file.PatientId).IsEqualTo(dara.Id);
        await Assert.That(file.FileName).IsEqualTo("referral.pdf");
        await Assert.That(file.StoredFileName).IsEqualTo($"{file.Id:N}.pdf");
        await Assert.That(file.SizeBytes).IsEqualTo(SampleFiles.Pdf.Length);
        await s3.GetObjectMetadataAsync(bucket, file.StoredFileName);
    }

    private static Task<Guid> ImportUserIdAsync(AppDbContext db) =>
        db.Users.Where(x => x.NormalizedUserName == "UPSTREAM-IMPORT").Select(x => x.Id).SingleAsync();

    private static async Task<Dictionary<string, int>> CountImportedRowsAsync(IHost host, JsonObject dataset)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var patientIds = dataset["patientsRouter.list"]!.AsArray().Select(x => Ids.Patient((int)x!["id"]!)).ToList();
        var visitIds = new[] { 720001, 720002, 720003 }.Select(Ids.Visit).ToList();
        return new Dictionary<string, int>
        {
            ["patients"] = await db.Patients.CountAsync(x => patientIds.Contains(x.Id)),
            ["visits"] = await db.Visits.CountAsync(x => visitIds.Contains(x.Id)),
            ["vitals"] = await db.Vitals.CountAsync(x => visitIds.Contains(x.VisitId)),
            ["eyesight"] = await db.Eyesights.CountAsync(x => visitIds.Contains(x.VisitId)),
            ["consults"] = await db.Consults.CountAsync(x => visitIds.Contains(x.VisitId)),
            ["diagnoses"] = await db.Diagnoses.CountAsync(x => visitIds.Contains(x.Consult.VisitId)),
            ["stock"] = await db.MedicationStocks.CountAsync(x => x.MedicationBrandId == Ids.Brand(790101)),
            ["stock changes"] = await db.StockChanges.CountAsync(x => x.Stock.MedicationBrandId == Ids.Brand(790101)),
            ["consents"] = await db.PatientConsents.CountAsync(x => patientIds.Contains(x.PatientId)),
            ["files"] = await db.Files.CountAsync(x => patientIds.Contains(x.PatientId!.Value)),
            ["staff"] = await db.Users.CountAsync(x => x.UserName!.StartsWith("upstream-")),
            ["audit"] = await db.AuditLogs.CountAsync(x => x.UserName == ImportActor.UserName),
        };
    }

    private async Task<IHost> BuildServicesAsync()
    {
        var fs = fixture.App.GetEndpoint("fs", "s3");
        return ImportServices.Build(
            [],
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:sabai"] = await fixture.App.GetConnectionStringAsync("sabai"),
                ["ConnectionStrings:qdrant"] = await fixture.App.GetConnectionStringAsync("qdrant"),
                ["ConnectionStrings:fs-sabai"] = $"Endpoint={fs};AccessKey=rustfsadmin;SecretKey=rustfsadmin;Bucket=sabai",
            }
        );
    }

    /// <summary>
    /// Stands in for the ONNX face model, which finds no face in the generated test images: a
    /// fixed 512-d embedding (the size of the bundled recognition model), stored in the real Qdrant.
    /// </summary>
    private sealed class FixedEmbedder : IFaceEmbedder
    {
        public Task<float[]?> EmbedLargestFaceAsync(RgbImage image, CancellationToken ct)
        {
            var embedding = new float[512];
            for (var i = 0; i < embedding.Length; i++)
            {
                embedding[i] = (float)Math.Sin(i + image.Width);
            }

            return Task.FromResult<float[]?>(embedding);
        }
    }
}

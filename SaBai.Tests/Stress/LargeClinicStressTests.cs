using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using NpgsqlTypes;
using SaBai.API.Data;
using SaBai.API.Domain;
using SaBai.Tests.Support;
using ConsultItem = SaBai.API.Endpoints.Consults.ListByVisit.Get.Response;
using EyesightItem = SaBai.API.Endpoints.Eyesight.ListByPatient.Get.Response;
using PatientListResponse = SaBai.API.Endpoints.Patients.List.Get.Response;
using PatientVisitsResponse = SaBai.API.Endpoints.Patients.Visits.Get.Response;
using WithLatestVisitResponse = SaBai.API.Endpoints.Patients.WithLatestVisit.Get.Response;

namespace SaBai.Tests.Stress;

/// <summary>
/// Runs the API against a clinic with 50,000 patients, each with 3 past visits
/// that each have a consult (with a diagnosis), an eyesight record and a dental
/// consult: 150,000 visits / consults / eyesight / dental records in all.
///
/// The data is bulk-loaded straight into Postgres with COPY (going through
/// the API would be ~600,000 requests) and is kept between runs: the Postgres
/// container uses a data volume, so seeding is skipped when the stress village
/// already holds the full set. Visits are dated in the past so they don't show
/// up in "today" views used by other tests.
///
/// Each test times an endpoint against a generous budget: these catch
/// queries that blow up with data volume (N+1s, unindexed scans, loading whole
/// tables), not small regressions.
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel(nameof(LargeClinicStressTests))]
[Category("Stress")]
public sealed class LargeClinicStressTests(AppFixture fixture)
{
    public const int PatientCount = 50_000;
    public const int VisitsPerPatient = 3;
    private const string VillageCode = "STRESS50K";
    private const string NamePrefix = "Stress Patient ";

    private static readonly SemaphoreSlim SeedLock = new(1, 1);
    private static StressData? seeded;

    [Test]
    [Timeout(15 * 60_000)]
    public async Task Seed_HasEveryRecord(CancellationToken ct)
    {
        // Arrange
        var data = await SeedAsync(ct);
        await using var db = await OpenAsync(ct);

        // Act
        var counts = await ScalarAsync<long[]>(
            db,
            """
            SELECT ARRAY[
              (SELECT count(*) FROM "Patients" WHERE "VillageCodeId" = @v),
              (SELECT count(*) FROM "Visits" WHERE "VillageCodeId" = @v),
              (SELECT count(*) FROM "Consults" c JOIN "Visits" v ON v."Id" = c."VisitId" WHERE v."VillageCodeId" = @v),
              (SELECT count(*) FROM "Eyesights" e JOIN "Visits" v ON v."Id" = e."VisitId" WHERE v."VillageCodeId" = @v),
              (SELECT count(*) FROM "DentalConsults" d JOIN "Visits" v ON v."Id" = d."VisitId" WHERE v."VillageCodeId" = @v)
            ]
            """,
            ct,
            ("v", data.VillageId)
        );

        // Assert
        const long visits = PatientCount * VisitsPerPatient;
        await Assert.That(counts).IsEquivalentTo(new long[] { PatientCount, visits, visits, visits, visits });
    }

    [Test]
    [Timeout(15 * 60_000)]
    public async Task WithLatestVisit_ListsEveryPatient(CancellationToken ct)
    {
        // Arrange
        var data = await SeedAsync(ct);
        var client = await ClientAsync();

        // Act
        var (body, elapsed) = await TimeAsync(() =>
            client.GetFromJsonAsync<WithLatestVisitResponse>("/patients/with-latest-visit", ApiJson.Options, ct)
        );
        var stress = body!.Patients.Where(x => x.VillageCodeId == data.VillageId).ToList();
        var sample = stress.Single(x => x.Id == data.LastPatientId);

        // Assert
        await Assert.That(stress.Count).IsEqualTo(PatientCount);
        await Assert.That(sample.LastVisitJourney).IsNotNull();
        await Assert.That(sample.LastVisitJourney!.Consult).IsTrue();
        await Assert.That(sample.LastVisitJourney.Vision).IsTrue();
        await Assert.That(sample.LastVisitJourney.Dental).IsTrue();
        await Assert.That(elapsed).IsLessThan(TimeSpan.FromSeconds(30));
    }

    [Test]
    [Timeout(15 * 60_000)]
    public async Task PatientList_FiltersByNameAndVillage(CancellationToken ct)
    {
        // Arrange
        var data = await SeedAsync(ct);
        var client = await ClientAsync();
        var name = Uri.EscapeDataString(NameOf(PatientCount - 1));

        // Act
        var (byName, nameElapsed) = await TimeAsync(() =>
            client.GetFromJsonAsync<PatientListResponse>($"/patients?name={name}", ApiJson.Options, ct)
        );
        var (byVillage, villageElapsed) = await TimeAsync(() =>
            client.GetFromJsonAsync<PatientListResponse>($"/patients?villageCode={VillageCode}", ApiJson.Options, ct)
        );

        // Assert
        await Assert.That(byName!.Patients.Select(x => x.Id)).IsEquivalentTo(new[] { data.LastPatientId });
        await Assert.That(byVillage!.Patients.Count).IsEqualTo(PatientCount);
        await Assert.That(nameElapsed).IsLessThan(TimeSpan.FromSeconds(5));
        await Assert.That(villageElapsed).IsLessThan(TimeSpan.FromSeconds(30));
    }

    [Test]
    [Timeout(15 * 60_000)]
    public async Task PatientScreens_StayFastForOnePatient(CancellationToken ct)
    {
        // Arrange
        var data = await SeedAsync(ct);
        var client = await ClientAsync();
        var patientId = data.LastPatientId;

        // Act: what opening one patient's record and a visit loads.
        var (visits, visitsElapsed) = await TimeAsync(() =>
            client.GetFromJsonAsync<PatientVisitsResponse>($"/patients/{patientId}/visits", ApiJson.Options, ct)
        );
        var visitId = visits!.Visits.First().Id;
        var (consults, consultsElapsed) = await TimeAsync(() =>
            client.GetFromJsonAsync<List<ConsultItem>>($"/visits/{visitId}/consults", ApiJson.Options, ct)
        );
        var (eyesight, eyesightElapsed) = await TimeAsync(() =>
            client.GetFromJsonAsync<List<EyesightItem>>($"/patients/{patientId}/eyesight", ApiJson.Options, ct)
        );
        var (records, recordsElapsed) = await TimeAsync(() => client.GetAsync($"/patient-records?visitId={visitId}", ct));
        var (dental, dentalElapsed) = await TimeAsync(() => client.GetAsync($"/visits/{visitId}/dental", ct));
        var (visitList, visitListElapsed) = await TimeAsync(() => client.GetAsync($"/visits?patientId={patientId}", ct));

        // Assert
        var perPatient = TimeSpan.FromSeconds(2);
        await Assert.That(visits.Visits.Count).IsEqualTo(VisitsPerPatient);
        await Assert.That(consults!.Count).IsEqualTo(1);
        await Assert.That(eyesight!.Count).IsEqualTo(VisitsPerPatient);
        await Assert.That(records.IsSuccessStatusCode).IsTrue();
        await Assert.That(dental.IsSuccessStatusCode).IsTrue();
        await Assert.That(visitList.IsSuccessStatusCode).IsTrue();
        await Assert.That(visitsElapsed).IsLessThan(perPatient);
        await Assert.That(consultsElapsed).IsLessThan(perPatient);
        await Assert.That(eyesightElapsed).IsLessThan(perPatient);
        await Assert.That(recordsElapsed).IsLessThan(perPatient);
        await Assert.That(dentalElapsed).IsLessThan(perPatient);
        await Assert.That(visitListElapsed).IsLessThan(perPatient);
    }

    [Test]
    [Timeout(15 * 60_000)]
    public async Task ConcurrentPatientLookups_AllSucceed(CancellationToken ct)
    {
        // Arrange: 25 staff each opening 8 random patients at once.
        var data = await SeedAsync(ct);
        var client = await ClientAsync();
        var random = new Random(42);
        var patientIds = Enumerable
            .Range(0, 200)
            .Select(_ => data.FirstPatientId + random.Next(PatientCount))
            .ToArray();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var responses = await Task.WhenAll(
            patientIds
                .Chunk(8)
                .Select(async chunk =>
                {
                    var results = new List<HttpStatusCode>();
                    foreach (var id in chunk)
                    {
                        using var response = await client.GetAsync($"/patients/{id}/visits", ct);
                        results.Add(response.StatusCode);
                    }
                    return results;
                })
        );
        stopwatch.Stop();

        // Assert
        await Assert.That(responses.SelectMany(x => x).All(x => x == HttpStatusCode.OK)).IsTrue();
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(30));
    }

    private async Task<HttpClient> ClientAsync()
    {
        var client = fixture.CreateHttpClient("api");
        client.Timeout = TimeSpan.FromMinutes(5);
        await client.LoginAsync();
        return client;
    }

    private static async Task<(T Result, TimeSpan Elapsed)> TimeAsync<T>(Func<Task<T>> action)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await action();
        stopwatch.Stop();
        Console.WriteLine($"[stress] {stopwatch.Elapsed.TotalMilliseconds:F0} ms");
        return (result, stopwatch.Elapsed);
    }

    private static string NameOf(int index) => $"{NamePrefix}{index:D5}";

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        var connectionString = await fixture.App.GetConnectionStringAsync("sabai", ct);
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private static async Task<T> ScalarAsync<T>(
        NpgsqlConnection db,
        string sql,
        CancellationToken ct,
        params (string Name, object Value)[] parameters
    )
    {
        await using var command = new NpgsqlCommand(sql, db);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        return (T)(await command.ExecuteScalarAsync(ct))!;
    }

    private sealed record StressData(int VillageId, int FirstPatientId, int LastPatientId);

    /// <summary>Seeds the stress clinic once per test session (and once per data volume).</summary>
    private async Task<StressData> SeedAsync(CancellationToken ct)
    {
        await SeedLock.WaitAsync(ct);
        try
        {
            return seeded ??= await LoadOrSeedAsync(ct);
        }
        finally
        {
            SeedLock.Release();
        }
    }

    private async Task<StressData> LoadOrSeedAsync(CancellationToken ct)
    {
        // Logging in seeds the staff accounts, so the doctor exists.
        var doctor = fixture.CreateHttpClient("api");
        await doctor.LoginAsync(ClinicApi.DoctorEmail);
        var doctorId = (await doctor.WhoAmIAsync()).Id;

        await using var db = await OpenAsync(ct);
        var villageId = await EnsureVillageAsync(db, ct);
        var existing = await ScalarAsync<long>(
            db,
            """SELECT count(*) FROM "Patients" WHERE "VillageCodeId" = @v""",
            ct,
            ("v", villageId)
        );
        if (existing != PatientCount)
        {
            if (existing != 0)
            {
                throw new InvalidOperationException(
                    $"Village {VillageCode} holds {existing} patients, expected 0 or {PatientCount}. "
                        + "Remove the Postgres data volume to reseed."
                );
            }
            await BulkLoadAsync(db, villageId, doctorId, ct);
        }

        await using var range = new NpgsqlCommand(
            """SELECT min("Id"), max("Id") FROM "Patients" WHERE "VillageCodeId" = @v""",
            db
        );
        range.Parameters.AddWithValue("v", villageId);
        await using var reader = await range.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return new StressData(villageId, reader.GetInt32(0), reader.GetInt32(1));
    }

    private static async Task<int> EnsureVillageAsync(NpgsqlConnection db, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            """
            WITH found AS (SELECT "Id" FROM "VillageCodes" WHERE "Code" = @code),
            created AS (
              INSERT INTO "VillageCodes" ("Code", "Name", "ColorHex", "IsVisible", "CreatedAt", "UpdatedAt")
              SELECT @code, 'Stress test village', '#999999', false, now(), now()
              WHERE NOT EXISTS (SELECT 1 FROM found)
              RETURNING "Id"
            )
            SELECT "Id" FROM found UNION ALL SELECT "Id" FROM created
            """,
            db
        );
        command.Parameters.AddWithValue("code", VillageCode);
        return (int)(await command.ExecuteScalarAsync(ct))!;
    }

    /// <summary>
    /// Loads patients, then visits, then each visit's records with binary COPY.
    /// Ids come from the database: each step reads back the ids it created by
    /// a natural key (patient name; patient + visit date) to link the next.
    /// Table and column names come from the EF model so they follow mappings.
    /// </summary>
    private static async Task BulkLoadAsync(NpgsqlConnection db, int villageId, Guid doctorId, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        await using var context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(db).Options
        );
        var model = context.Model;
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await db.BeginTransactionAsync(ct);

        // Patients
        await CopyAsync(
            db,
            model,
            typeof(Patient),
            [
                nameof(Patient.Name),
                nameof(Patient.VillageCodeId),
                nameof(Patient.Gender),
                nameof(Patient.DateOfBirth),
                nameof(Patient.DrugAllergy),
                nameof(Patient.HasPoorCard),
                nameof(Patient.HasBS2Card),
                nameof(Patient.HasSabaiCard),
                nameof(Patient.ToGetReport),
                nameof(Patient.ContactNo),
                nameof(Patient.CreatedAt),
            ],
            Enumerable.Range(0, PatientCount),
            async (w, i) =>
            {
                await w.WriteAsync(NameOf(i), NpgsqlDbType.Text, ct);
                await w.WriteAsync(villageId, NpgsqlDbType.Integer, ct);
                await WriteEnumAsync(w, model, i % 2 == 0 ? Gender.Female : Gender.Male, ct);
                await w.WriteAsync(now.AddYears(-5 - i % 70).AddDays(-(i % 365)), NpgsqlDbType.TimestampTz, ct);
                await w.WriteAsync(i % 10 == 0 ? "Penicillin" : "None", NpgsqlDbType.Text, ct);
                await w.WriteAsync(i % 3 == 0, NpgsqlDbType.Boolean, ct);
                await w.WriteAsync(i % 5 == 0, NpgsqlDbType.Boolean, ct);
                await w.WriteAsync(i % 7 == 0, NpgsqlDbType.Boolean, ct);
                await w.WriteAsync(false, NpgsqlDbType.Boolean, ct);
                await w.WriteAsync($"08{i:D8}", NpgsqlDbType.Text, ct);
                await w.WriteAsync(now.AddYears(-3), NpgsqlDbType.TimestampTz, ct);
            },
            ct
        );
        var patientIds = await ReadIdsAsync(
            db,
            """SELECT "Id" FROM "Patients" WHERE "VillageCodeId" = @v ORDER BY "Name" """,
            villageId,
            ct
        );

        // Visits: three per patient, a year apart, ending well before today.
        var visits = patientIds
            .SelectMany((patientId, i) =>
                Enumerable
                    .Range(0, VisitsPerPatient)
                    .Select(n => (PatientId: patientId, Date: now.AddDays(-30 - 365 * (VisitsPerPatient - 1 - n) - i % 300)))
            )
            .ToList();
        await CopyAsync(
            db,
            model,
            typeof(Visit),
            [nameof(Visit.PatientId), nameof(Visit.VillageCodeId), nameof(Visit.Date), nameof(Visit.Status)],
            visits,
            async (w, v) =>
            {
                await w.WriteAsync(v.PatientId, NpgsqlDbType.Integer, ct);
                await w.WriteAsync(villageId, NpgsqlDbType.Integer, ct);
                await w.WriteAsync(v.Date, NpgsqlDbType.TimestampTz, ct);
                await w.WriteAsync(Visit.StartedStatus, NpgsqlDbType.Text, ct);
            },
            ct
        );
        var visitRows = new List<(int Id, DateTimeOffset Date)>(visits.Count);
        await using (
            var command = new NpgsqlCommand(
                """SELECT "Id", "Date" FROM "Visits" WHERE "VillageCodeId" = @v""",
                db,
                transaction
            )
        )
        {
            command.Parameters.AddWithValue("v", villageId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                visitRows.Add((reader.GetInt32(0), reader.GetFieldValue<DateTimeOffset>(1)));
            }
        }

        // Each visit: a consult with a diagnosis, eyesight and dental, saved
        // over the visit's morning.
        await CopyAsync(
            db,
            model,
            typeof(Consult),
            [nameof(Consult.VisitId), nameof(Consult.Date), nameof(Consult.DoctorId), nameof(Consult.Consultation), nameof(Consult.TreatmentPlan)],
            visitRows,
            async (w, v) =>
            {
                await w.WriteAsync(v.Id, NpgsqlDbType.Integer, ct);
                await w.WriteAsync(v.Date.AddMinutes(40), NpgsqlDbType.TimestampTz, ct);
                await w.WriteAsync(doctorId, NpgsqlDbType.Uuid, ct);
                await w.WriteAsync("Routine check-up; chest clear, no acute complaints.", NpgsqlDbType.Text, ct);
                await w.WriteAsync("Hydration, follow up next visit.", NpgsqlDbType.Text, ct);
            },
            ct
        );
        var consultIds = await ReadIdsAsync(
            db,
            """SELECT c."Id" FROM "Consults" c JOIN "Visits" v ON v."Id" = c."VisitId" WHERE v."VillageCodeId" = @v""",
            villageId,
            ct
        );
        await CopyAsync(
            db,
            model,
            typeof(Diagnosis),
            [nameof(Diagnosis.ConsultId), nameof(Diagnosis.Details), nameof(Diagnosis.Category)],
            consultIds,
            async (w, id) =>
            {
                await w.WriteAsync(id, NpgsqlDbType.Integer, ct);
                await w.WriteAsync("Upper respiratory tract infection", NpgsqlDbType.Text, ct);
                await WriteEnumAsync(w, model, DiagnosisCategory.Respiratory, ct);
            },
            ct
        );
        await CopyAsync(
            db,
            model,
            typeof(Eyesight),
            [nameof(Eyesight.VisitId), nameof(Eyesight.RecordedAt), nameof(Eyesight.LeftEyeDegree), nameof(Eyesight.RightEyeDegree), nameof(Eyesight.Comments)],
            visitRows,
            async (w, v) =>
            {
                await w.WriteAsync(v.Id, NpgsqlDbType.Integer, ct);
                await w.WriteAsync(v.Date.AddMinutes(20), NpgsqlDbType.TimestampTz, ct);
                await w.WriteAsync("6/9", NpgsqlDbType.Text, ct);
                await w.WriteAsync("6/12", NpgsqlDbType.Text, ct);
                await w.WriteAsync("Mild myopia", NpgsqlDbType.Text, ct);
            },
            ct
        );
        await CopyAsync(
            db,
            model,
            typeof(DentalConsult),
            [nameof(DentalConsult.VisitId), nameof(DentalConsult.RecordedAt), nameof(DentalConsult.UpdatedAt), nameof(DentalConsult.Diagnoses), nameof(DentalConsult.ChiefComplaint), nameof(DentalConsult.Toothache)],
            visitRows,
            async (w, v) =>
            {
                await w.WriteAsync(v.Id, NpgsqlDbType.Integer, ct);
                await w.WriteAsync(v.Date.AddMinutes(60), NpgsqlDbType.TimestampTz, ct);
                await w.WriteAsync(v.Date.AddMinutes(60), NpgsqlDbType.TimestampTz, ct);
                await w.WriteAsync(new List<string> { "Caries" }, NpgsqlDbType.Array | NpgsqlDbType.Text, ct);
                await w.WriteAsync("Sensitive molar", NpgsqlDbType.Text, ct);
                await w.WriteAsync(true, NpgsqlDbType.Boolean, ct);
            },
            ct
        );

        await transaction.CommitAsync(ct);
        await using (var analyze = new NpgsqlCommand("ANALYZE", db))
        {
            await analyze.ExecuteNonQueryAsync(ct);
        }
        Console.WriteLine($"[stress] seeded {PatientCount} patients in {stopwatch.Elapsed.TotalSeconds:F1} s");
    }

    private static async Task<List<int>> ReadIdsAsync(NpgsqlConnection db, string sql, int villageId, CancellationToken ct)
    {
        var ids = new List<int>();
        await using var command = new NpgsqlCommand(sql, db);
        command.Parameters.AddWithValue("v", villageId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            ids.Add(reader.GetInt32(0));
        }
        return ids;
    }

    private static async Task CopyAsync<TRow>(
        NpgsqlConnection db,
        IModel model,
        Type entityType,
        string[] properties,
        IEnumerable<TRow> rows,
        Func<NpgsqlBinaryImporter, TRow, Task> write,
        CancellationToken ct
    )
    {
        var entity = model.FindEntityType(entityType)!;
        var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
        var columns = properties.Select(p => $"\"{entity.FindProperty(p)!.GetColumnName(table)}\"");
        var sql = $"COPY \"{table.Name}\" ({string.Join(", ", columns)}) FROM STDIN (FORMAT BINARY)";
        await using var importer = await db.BeginBinaryImportAsync(sql, ct);
        foreach (var row in rows)
        {
            await importer.StartRowAsync(ct);
            await write(importer, row);
        }
        await importer.CompleteAsync(ct);
    }

    /// <summary>Writes an enum the way the EF model stores it (string or int).</summary>
    private static async Task WriteEnumAsync<TEnum>(NpgsqlBinaryImporter w, IModel model, TEnum value, CancellationToken ct)
        where TEnum : struct, Enum
    {
        var property = model
            .GetEntityTypes()
            .SelectMany(x => x.GetProperties())
            .First(x => x.ClrType == typeof(TEnum) || x.ClrType == typeof(TEnum?));
        var provider = property.GetTypeMapping().Converter?.ProviderClrType ?? typeof(int);
        if (provider == typeof(string))
        {
            var converted = property.GetTypeMapping().Converter!.ConvertToProvider(value);
            await w.WriteAsync((string)converted!, NpgsqlDbType.Text, ct);
        }
        else
        {
            await w.WriteAsync(Convert.ToInt32(value), NpgsqlDbType.Integer, ct);
        }
    }
}

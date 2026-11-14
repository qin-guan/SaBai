using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;
using SaBai.API.Sync;

namespace SaBai.Tests.Sync;

public sealed class SyncUploadProcessorTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"sync-test-{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static ClaimsPrincipal StaffPrincipal(Guid userId, string role = StaffRoles.Member) =>
        new(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Role, role),
                ],
                "test"
            )
        );

    private static JsonElement Json(object value) =>
        JsonSerializer.SerializeToElement(value);

    [Test]
    public async Task Put_Patient_Is_Idempotent_On_Replay()
    {
        await using var db = CreateDb();
        var processor = new SyncUploadProcessor(db);
        var user = StaffPrincipal(Guid.CreateVersion7());
        var patientId = Guid.CreateVersion7();

        var op = new SyncCrudOp(
            "PUT",
            "patients",
            patientId,
            Json(new { name = "Sokha Meas", gender = "Female", date_of_birth = "1995-04-12T00:00:00Z" }),
            OpId: 1
        );

        var r1 = await processor.ProcessAsync([op], user, CancellationToken.None);
        var r2 = await processor.ProcessAsync([op], user, CancellationToken.None);

        await Assert.That(r1.Applied).IsEqualTo(1);
        await Assert.That(r2.Applied).IsEqualTo(1);
        await Assert.That(await db.Patients.CountAsync()).IsEqualTo(1);
    }

    [Test]
    public async Task Natural_Key_Upsert_Merges_Vitals_For_Same_Visit()
    {
        await using var db = CreateDb();
        var patient = new Patient { Name = "Dara", Gender = Gender.Male, DateOfBirth = DateTimeOffset.UtcNow, DrugAllergy = "None" };
        var visit = new Visit { PatientId = patient.Id, Date = DateTimeOffset.UtcNow, Status = Visit.StartedStatus };
        db.Patients.Add(patient);
        db.Visits.Add(visit);
        await db.SaveChangesAsync();

        var processor = new SyncUploadProcessor(db);
        var user = StaffPrincipal(Guid.CreateVersion7());

        var op1 = new SyncCrudOp("PUT", "vitals", Guid.CreateVersion7(), Json(new { visit_id = visit.Id, height = 165.0m }), OpId: 1);
        var op2 = new SyncCrudOp("PUT", "vitals", Guid.CreateVersion7(), Json(new { visit_id = visit.Id, weight = 60.5m }), OpId: 2);

        var res = await processor.ProcessAsync([op1, op2], user, CancellationToken.None);

        await Assert.That(res.Applied).IsEqualTo(2);
        await Assert.That(await db.Vitals.CountAsync()).IsEqualTo(1);
        var vitals = await db.Vitals.SingleAsync();
        await Assert.That(vitals.Height).IsEqualTo(165.0m);
        await Assert.That(vitals.Weight).IsEqualTo(60.5m);
    }

    [Test]
    public async Task Stale_Patch_Logs_SyncConflict_On_Overwritten_Field()
    {
        await using var db = CreateDb();
        var patient = new Patient { Name = "Initial Name", Gender = Gender.Female, DateOfBirth = DateTimeOffset.UtcNow, DrugAllergy = "None" };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        // Bump version on server to simulate another device's edit
        patient.Name = "Server Updated";
        await db.SaveChangesAsync();
        await Assert.That(patient.Version).IsEqualTo(2);

        var processor = new SyncUploadProcessor(db);
        var user = StaffPrincipal(Guid.CreateVersion7());

        var stalePatch = new SyncCrudOp(
            "PATCH",
            "patients",
            patient.Id,
            Json(new { name = "Offline Device Name" }),
            OpId: 10,
            BaseVersion: 1
        );

        var res = await processor.ProcessAsync([stalePatch], user, CancellationToken.None);

        await Assert.That(res.Applied).IsEqualTo(1);
        await Assert.That(res.Conflicts).IsEqualTo(1);
        var updated = await db.Patients.SingleAsync();
        await Assert.That(updated.Name).IsEqualTo("Offline Device Name");
        var conflict = await db.SyncConflicts.SingleAsync();
        await Assert.That(conflict.PreviousValue).IsEqualTo("Server Updated");
        await Assert.That(conflict.IncomingValue).IsEqualTo("Offline Device Name");
    }

    [Test]
    public async Task Consult_Past_24h_Window_Records_SyncRejection_Without_Failing_Batch()
    {
        await using var db = CreateDb();
        var patient = new Patient { Name = "Chan", Gender = Gender.Male, DateOfBirth = DateTimeOffset.UtcNow, DrugAllergy = "None" };
        var visit = new Visit { PatientId = patient.Id, Date = DateTimeOffset.UtcNow.AddDays(-2), Status = "completed" };
        var consult = new Consult
        {
            VisitId = visit.Id,
            DoctorId = Guid.CreateVersion7(),
            Date = DateTimeOffset.UtcNow.AddHours(-30),
            Consultation = "Original note",
        };
        db.Patients.Add(patient);
        db.Visits.Add(visit);
        db.Consults.Add(consult);
        await db.SaveChangesAsync();

        var processor = new SyncUploadProcessor(db);
        var user = StaffPrincipal(Guid.CreateVersion7());

        var expiredEdit = new SyncCrudOp(
            "PATCH",
            "consults",
            consult.Id,
            Json(new { consultation = "Late offline edit" }),
            OpId: 1
        );
        var validPatient = new SyncCrudOp(
            "PUT",
            "patients",
            Guid.CreateVersion7(),
            Json(new { name = "Still Applied", gender = "Female", date_of_birth = "2000-01-01T00:00:00Z" }),
            OpId: 2
        );

        var res = await processor.ProcessAsync([expiredEdit, validPatient], user, CancellationToken.None);

        await Assert.That(res.Applied).IsEqualTo(1);
        await Assert.That(res.Rejections.Count).IsEqualTo(1);
        await Assert.That(await db.SyncRejections.CountAsync()).IsEqualTo(1);
        await Assert.That((await db.Consults.SingleAsync()).Consultation).IsEqualTo("Original note");
    }

    [Test]
    public async Task SyncTokenService_Mints_Jwt_And_Jwks()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        using var svc = new SyncTokenService(config);

        var userId = Guid.CreateVersion7();
        var (token, expiresAt) = svc.CreateToken(userId, [StaffRoles.Admin]);
        var jwks = svc.GetJwksJson();

        await Assert.That(token.Split('.').Length).IsEqualTo(3);
        await Assert.That(expiresAt > DateTimeOffset.UtcNow).IsTrue();
        await Assert.That(jwks.Contains("sabai-sync-1")).IsTrue();
    }
}

using System.Net;
using System.Net.Http.Json;
using SaBai.API.Domain;
using SaBai.Tests.Patients;
using SaBai.Tests.Support;
using AuditLogResponse = SaBai.API.Endpoints.AuditLogs.List.Get.Response;

namespace SaBai.Tests.Audit;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class AuditLogTests(AppFixture fixture)
{
    [Test]
    public async Task RecordChanges_AreLoggedWithUserAndOldAndNewValues()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var me = await client.WhoAmIAsync();
        var code = ClinicSamples.Code();
        var village = await ClinicSamples.CreateVillageAsync(client, code, name: "Before");

        // Act
        var updated = await client.PatchAsJsonAsync(
            $"/village-codes/{village.Id}",
            new { name = "After" },
            ApiJson.Options
        );
        var deleted = await client.DeleteAsync($"/village-codes/{village.Id}");
        var log = await client.GetFromJsonAsync<AuditLogResponse>(
            $"/audit-logs?entityType=VillageCode&entityId={village.Id}",
            ApiJson.Options
        );

        // Assert
        await Assert.That(updated.IsSuccessStatusCode).IsTrue();
        await Assert.That(deleted.IsSuccessStatusCode).IsTrue();
        var actions = log!.Entries.Select(x => x.Action).ToList();
        await Assert
            .That(actions)
            .IsEquivalentTo(new[] { AuditAction.Deleted, AuditAction.Updated, AuditAction.Created });
        await Assert.That(log.Entries.All(x => x.UserId == me.Id)).IsTrue();
        await Assert.That(log.Entries.All(x => x.UserName == me.UserName)).IsTrue();

        var created = log.Entries.Single(x => x.Action == AuditAction.Created);
        await Assert.That(created.Changes.Single(x => x.Field == "Code").New).IsEqualTo(code);
        await Assert.That(created.Request).IsEqualTo("POST /village-codes");

        var rename = log.Entries.Single(x => x.Action == AuditAction.Updated).Changes.Single(x =>
            x.Field == "Name"
        );
        await Assert.That(rename.Old).IsEqualTo("Before");
        await Assert.That(rename.New).IsEqualTo("After");

        var removed = log.Entries.Single(x => x.Action == AuditAction.Deleted);
        await Assert.That(removed.Changes.Single(x => x.Field == "Name").Old).IsEqualTo("After");
    }

    [Test]
    public async Task SignIns_FailuresAndSignOut_AreLogged()
    {
        // Arrange
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var anonymous = fixture.CreateApiClient();
        var unknownName = $"ghost-{Guid.NewGuid():N}";

        // Act
        var wrongPassword = await anonymous.PostAsJsonAsync(
            "/auth/login",
            new { userName = "registration", password = "wrong-password" },
            ApiJson.Options
        );
        var unknownUser = await anonymous.PostAsJsonAsync(
            "/auth/login",
            new { userName = unknownName, password = "wrong-password" },
            ApiJson.Options
        );
        var registration = fixture.CreateApiClient();
        await registration.LoginAsync(ClinicApi.RegistrationEmail);
        var me = await registration.WhoAmIAsync();
        var signedOut = await registration.PostAsync("/auth/logout", null);
        var mine = await admin.GetFromJsonAsync<AuditLogResponse>(
            $"/audit-logs?userId={me.Id}&take=200",
            ApiJson.Options
        );
        var failures = await admin.GetFromJsonAsync<AuditLogResponse>(
            "/audit-logs?action=SignInFailed&take=200",
            ApiJson.Options
        );

        // Assert
        await Assert.That(wrongPassword.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(unknownUser.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(signedOut.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(mine!.Entries.Any(x => x.Action == AuditAction.SignedIn)).IsTrue();
        await Assert.That(mine.Entries.Any(x => x.Action == AuditAction.SignedOut)).IsTrue();
        await Assert
            .That(mine.Entries.Any(x => x.Action == AuditAction.SignInFailed && x.UserId == me.Id))
            .IsTrue();
        await Assert
            .That(failures!.Entries.Any(x => x.UserId == null && x.UserName == unknownName))
            .IsTrue();
    }

    [Test]
    public async Task Secrets_AreRedacted()
    {
        // Arrange
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var userName = $"audit{Guid.NewGuid():N}"[..20];
        var create = await admin.PostAsJsonAsync(
            "/auth/users",
            new
            {
                userName,
                nickname = "Audit Target",
                password = "Password1!",
                roles = new[] { "member" },
            },
            ApiJson.Options
        );
        create.EnsureSuccessStatusCode();
        var user = await create.Content.ReadFromJsonAsync<SaBai.API.Endpoints.Auth.UserResponse>(
            ApiJson.Options
        );

        // Act
        var reset = await admin.PostAsJsonAsync(
            $"/auth/users/{user!.Id}/password",
            new { password = "Another-Password1!" },
            ApiJson.Options
        );
        var log = await admin.GetFromJsonAsync<AuditLogResponse>(
            $"/audit-logs?entityType=StaffUser&entityId={user.Id}",
            ApiJson.Options
        );

        // Assert
        await Assert.That(reset.IsSuccessStatusCode).IsTrue();
        var passwordChanges = log!
            .Entries.SelectMany(x => x.Changes)
            .Where(x => x.Field is "PasswordHash" or "SecurityStamp")
            .ToList();
        await Assert.That(passwordChanges).IsNotEmpty();
        await Assert
            .That(
                passwordChanges.All(x =>
                    (x.New is null or "[redacted]") && (x.Old is null or "[redacted]")
                )
            )
            .IsTrue();
    }

    [Test]
    public async Task List_IsAdminOnlyAndPagesWithCursor()
    {
        // Arrange
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var doctor = fixture.CreateApiClient();
        await doctor.LoginAsync(ClinicApi.DoctorEmail);
        var anonymous = fixture.CreateApiClient();

        // Act
        var forbidden = await doctor.GetAsync("/audit-logs");
        var unauthorized = await anonymous.GetAsync("/audit-logs");
        var badTake = await admin.GetAsync("/audit-logs?take=0");
        var first = await admin.GetFromJsonAsync<AuditLogResponse>(
            "/audit-logs?take=2",
            ApiJson.Options
        );
        var second = await admin.GetFromJsonAsync<AuditLogResponse>(
            $"/audit-logs?take=2&before={first!.NextBefore}",
            ApiJson.Options
        );

        // Assert
        await Assert.That(forbidden.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(unauthorized.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(badTake.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(first.Entries.Count).IsEqualTo(2);
        await Assert.That(first.NextBefore).IsEqualTo(first.Entries[^1].Id);
        await Assert.That(second!.Entries.All(x => x.Id < first.Entries[^1].Id)).IsTrue();
    }
}

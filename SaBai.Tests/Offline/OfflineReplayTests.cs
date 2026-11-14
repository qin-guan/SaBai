using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Offline;

/// <summary>
/// What a device replaying queued writes relies on: creates take the client's id and can be
/// resent safely, and <c>If-Unmodified-Since</c> refuses to overwrite another user's later edit.
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class OfflineReplayTests(AppFixture fixture)
{
    [Test]
    public async Task RegisterPatient_WithClientIds_UsesThem()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var (patientId, visitId, vitalsId) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        // Act
        var response = await client.PostAsJsonAsync(
            "/patients",
            Registration(villageId, patientId, visitId, vitalsId),
            ApiJson.Options
        );
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var vitals = await client.GetAsync($"/vitals/{vitalsId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(ClinicSetup.ReadGuid(body, "id")).IsEqualTo(patientId);
        await Assert.That(ClinicSetup.ReadGuid(body, "visitId")).IsEqualTo(visitId);
        await Assert.That(ClinicSetup.ReadGuid(body, "vitalsId")).IsEqualTo(vitalsId);
        await Assert.That(vitals.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task RegisterPatient_Resent_ReturnsSamePatientWithoutDuplicating()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var (patientId, visitId, vitalsId) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        var request = Registration(villageId, patientId, visitId, vitalsId);
        var first = await client.PostAsJsonAsync("/patients", request, ApiJson.Options);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Act
        var second = await client.PostAsJsonAsync("/patients", request, ApiJson.Options);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var visits = await client.GetFromJsonAsync<JsonElement>(
            $"/patients/{patientId}/visits",
            ApiJson.Options
        );

        // Assert
        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(ClinicSetup.ReadGuid(secondBody, "id")).IsEqualTo(patientId);
        await Assert.That(ClinicSetup.ReadGuid(secondBody, "visitId")).IsEqualTo(visitId);
        await Assert.That(ClinicSetup.ReadGuid(secondBody, "vitalsId")).IsEqualTo(vitalsId);
        await Assert.That(secondBody.GetProperty("displayId").GetString())
            .IsEqualTo(firstBody.GetProperty("displayId").GetString());
        await Assert.That(visits.GetProperty("visits").GetArrayLength()).IsEqualTo(1);
    }

    [Test]
    public async Task CreateVitals_Resent_ReturnsSameRecord()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var (_, visitId) = await ClinicSetup.RegisterPatientAsync(client, villageId);
        var id = Guid.CreateVersion7();
        var request = new { id, visitId, temperature = 36.6m };
        var first = await client.PostAsJsonAsync("/vitals", request, ApiJson.Options);

        // Act
        var second = await client.PostAsJsonAsync("/vitals", request, ApiJson.Options);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(ClinicSetup.ReadGuid(body, "id")).IsEqualTo(id);
        await Assert.That(body.GetProperty("temperature").GetDecimal()).IsEqualTo(36.6m);
    }

    [Test]
    public async Task CreateVitals_IdUsedOnAnotherVisit_ReturnsConflict()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var (_, firstVisitId) = await ClinicSetup.RegisterPatientAsync(client, villageId);
        var (_, secondVisitId) = await ClinicSetup.RegisterPatientAsync(client, villageId);
        var id = Guid.CreateVersion7();
        (await client.PostAsJsonAsync("/vitals", new { id, visitId = firstVisitId }, ApiJson.Options))
            .EnsureSuccessStatusCode();

        // Act
        var response = await client.PostAsJsonAsync(
            "/vitals",
            new { id, visitId = secondVisitId },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Create_EmptyClientId_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var (_, visitId) = await ClinicSetup.RegisterPatientAsync(client, villageId);

        // Act
        var response = await client.PostAsJsonAsync(
            "/vitals",
            new { id = Guid.Empty, visitId },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Update_IfUnmodifiedSinceBeforeOtherUsersEdit_ReturnsPreconditionFailed()
    {
        // Arrange
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var doctor = fixture.CreateApiClient();
        await doctor.LoginAsync(ClinicApi.DoctorEmail);
        var vitalsId = await CreateVitalsAsync(admin);
        var queuedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        (await doctor.PatchAsJsonAsync($"/vitals/{vitalsId}", new { temperature = 37.2m }, ApiJson.Options))
            .EnsureSuccessStatusCode();

        // Act
        var response = await PatchSinceAsync(admin, vitalsId, 38.0m, queuedAt);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var current = await admin.GetFromJsonAsync<JsonElement>($"/vitals/{vitalsId}", ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.PreconditionFailed);
        await Assert.That(body.GetProperty("status").GetInt32()).IsEqualTo(412);
        await Assert.That(body.GetProperty("recordType").GetString()).IsEqualTo("Vitals");
        await Assert.That(body.GetProperty("recordId").GetString()).IsEqualTo(vitalsId.ToString());
        await Assert.That(current.GetProperty("temperature").GetDecimal()).IsEqualTo(37.2m);
    }

    [Test]
    public async Task Update_IfUnmodifiedSinceAfterOwnEdit_Succeeds()
    {
        // Arrange
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var vitalsId = await CreateVitalsAsync(admin);
        var queuedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        (await admin.PatchAsJsonAsync($"/vitals/{vitalsId}", new { temperature = 37.2m }, ApiJson.Options))
            .EnsureSuccessStatusCode();

        // Act
        var response = await PatchSinceAsync(admin, vitalsId, 38.0m, queuedAt);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Update_WithoutIfUnmodifiedSince_OverwritesOtherUsersEdit()
    {
        // Arrange
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var doctor = fixture.CreateApiClient();
        await doctor.LoginAsync(ClinicApi.DoctorEmail);
        var vitalsId = await CreateVitalsAsync(admin);
        (await doctor.PatchAsJsonAsync($"/vitals/{vitalsId}", new { temperature = 37.2m }, ApiJson.Options))
            .EnsureSuccessStatusCode();

        // Act
        var response = await admin.PatchAsJsonAsync(
            $"/vitals/{vitalsId}",
            new { temperature = 38.0m },
            ApiJson.Options
        );
        var current = await admin.GetFromJsonAsync<JsonElement>($"/vitals/{vitalsId}", ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(current.GetProperty("temperature").GetDecimal()).IsEqualTo(38.0m);
    }

    private static object Registration(Guid villageId, Guid id, Guid visitId, Guid vitalsId) =>
        new
        {
            id,
            visitId,
            vitalsId,
            name = $"Patient {ClinicSetup.Suffix()}",
            gender = "female",
            dateOfBirth = new DateTimeOffset(1991, 3, 4, 0, 0, 0, TimeSpan.Zero),
            drugAllergy = "None",
            villageCodeId = villageId,
            vitals = new { temperature = 36.5m },
        };

    private static async Task<Guid> CreateVitalsAsync(HttpClient client)
    {
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var (_, visitId) = await ClinicSetup.RegisterPatientAsync(client, villageId);
        var response = await client.PostAsJsonAsync(
            "/vitals",
            new { visitId, temperature = 36.6m },
            ApiJson.Options
        );
        response.EnsureSuccessStatusCode();
        return ClinicSetup.ReadGuid(
            await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );
    }

    private static async Task<HttpResponseMessage> PatchSinceAsync(
        HttpClient client,
        Guid vitalsId,
        decimal temperature,
        DateTimeOffset since
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/vitals/{vitalsId}")
        {
            Content = JsonContent.Create(new { temperature }, options: ApiJson.Options),
        };
        request.Headers.IfUnmodifiedSince = since;
        return await client.SendAsync(request);
    }
}

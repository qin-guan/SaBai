using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Registry;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class VisitLifecycleTests(AppFixture fixture)
{
    [Test]
    public async Task Patch_StatusCompleted_IsReturnedOnGet()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var villageId = await ClinicSetup.CreateVillageAsync(client, $"V{suffix}");
        var (_, visitId) = await ClinicSetup.RegisterPatientAsync(client, villageId, $"Patient {suffix}");

        // Act
        var patched = await client.PatchAsJsonAsync(
            $"/visits/{visitId}",
            new { status = "completed" },
            ApiJson.Options);
        var fetched = await client.GetAsync($"/visits/{visitId}");
        var body = await fetched.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(patched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(fetched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).Contains("completed");
    }

    [Test]
    public async Task ListByPatient_ContainsRegistrationVisit()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var villageId = await ClinicSetup.CreateVillageAsync(client, $"V{suffix}");
        var (patientId, visitId) = await ClinicSetup.RegisterPatientAsync(client, villageId, $"Patient {suffix}");

        // Act
        var response = await client.GetAsync($"/patients/{patientId}/visits");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.GetProperty("visits").EnumerateArray().Any(x => ClinicSetup.ReadInt(x, "id") == visitId)).IsTrue();
    }

    [Test]
    public async Task Create_MissingPatient_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var villageId = await ClinicSetup.CreateVillageAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            "/visits",
            new { patientId = 99999999, villageCodeId = villageId },
            ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Get_MissingVisit_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.GetAsync("/visits/99999999");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Delete_WhenConsultExists_ReturnsConflict()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var villageId = await ClinicSetup.CreateVillageAsync(client, $"V{suffix}");
        var (_, visitId) = await ClinicSetup.RegisterPatientAsync(client, villageId, $"Patient {suffix}");
        await ClinicSetup.CreateConsultAsync(client, visitId);

        // Act
        var response = await client.DeleteAsync($"/visits/{visitId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");

        // Act
        var response = await client.PostAsJsonAsync(
            "/visits",
            new { patientId = 1, villageCodeId = 1 },
            ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}

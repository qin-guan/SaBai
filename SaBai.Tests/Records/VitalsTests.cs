using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Records;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class VitalsTests(AppFixture fixture)
{
    [Test]
    public async Task Create_ThenGet_ReturnsTemperature()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);

        // Act
        var created = await client.PostAsJsonAsync(
            "/vitals",
            new { visitId, temperature = 36.6m },
            ApiJson.Options);
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var id = ClinicSetup.ReadInt(createdBody, "id");
        var fetched = await client.GetAsync($"/vitals/{id}");
        var body = await fetched.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(fetched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.GetProperty("temperature").GetDecimal()).IsEqualTo(36.6m);
    }

    [Test]
    public async Task ListByPatient_IncludesCreatedId()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (patientId, visitId) = await RegisterAsync(client);
        var created = await client.PostAsJsonAsync(
            "/vitals",
            new { visitId, temperature = 36.6m },
            ApiJson.Options);
        created.EnsureSuccessStatusCode();
        var id = ClinicSetup.ReadInt(await created.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options), "id");

        // Act
        var listed = await client.GetAsync($"/patients/{patientId}/vitals");
        var items = await listed.Content.ReadFromJsonAsync<List<JsonElement>>(ApiJson.Options);

        // Assert
        await Assert.That(listed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(items!.Any(x => ClinicSetup.ReadInt(x, "id") == id)).IsTrue();
    }

    [Test]
    public async Task Create_UnknownVisit_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.PostAsJsonAsync(
            "/vitals",
            new { visitId = 99999999, temperature = 36.6m },
            ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetByVisit_UnknownVisit_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.GetAsync("/visits/99999999/vitals");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Patch_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.PatchAsJsonAsync(
            "/vitals/99999999",
            new { temperature = 37.1m },
            ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");

        // Act
        var response = await client.PostAsJsonAsync(
            "/vitals",
            new { visitId = 1, temperature = 36.6m },
            ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    private static async Task<(int PatientId, int VisitId)> RegisterAsync(HttpClient client)
    {
        var suffix = ClinicSetup.Suffix();
        var villageId = await ClinicSetup.CreateVillageAsync(client, $"V{suffix}");
        return await ClinicSetup.RegisterPatientAsync(client, villageId, $"Vitals {suffix}");
    }
}

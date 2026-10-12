using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Records;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class ChildVitalsTests(AppFixture fixture)
{
    [Test]
    public async Task Create_ThenGetByVitals_ReturnsHeart()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);
        var vitalsId = await CreateVitalsAsync(client, visitId);

        // Act
        var created = await client.PostAsJsonAsync(
            "/child-vitals",
            new
            {
                vitalsId,
                heart = "normal",
                pallor = false,
            },
            ApiJson.Options
        );
        var fetched = await client.GetAsync($"/vitals/{vitalsId}/child-vitals");
        var body = await fetched.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(fetched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.GetProperty("heart").GetString()).IsEqualTo("normal");
    }

    [Test]
    public async Task Create_UnknownVitals_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.PostAsJsonAsync(
            "/child-vitals",
            new
            {
                vitalsId = 99999999,
                heart = "normal",
                pallor = false,
            },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Create_SecondTime_ReturnsConflict()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);
        var vitalsId = await CreateVitalsAsync(client, visitId);
        var created = await client.PostAsJsonAsync(
            "/child-vitals",
            new
            {
                vitalsId,
                heart = "normal",
                pallor = false,
            },
            ApiJson.Options
        );
        created.EnsureSuccessStatusCode();

        // Act
        var again = await client.PostAsJsonAsync(
            "/child-vitals",
            new
            {
                vitalsId,
                heart = "murmur",
                pallor = true,
            },
            ApiJson.Options
        );

        // Assert
        await Assert.That(again.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Patch_ThenGet_ReturnsUpdatedHeart()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);
        var vitalsId = await CreateVitalsAsync(client, visitId);
        var created = await client.PostAsJsonAsync(
            "/child-vitals",
            new
            {
                vitalsId,
                heart = "normal",
                pallor = false,
            },
            ApiJson.Options
        );
        created.EnsureSuccessStatusCode();
        var id = ClinicSetup.ReadInt(
            await created.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );

        // Act
        var patched = await client.PatchAsJsonAsync(
            $"/child-vitals/{id}",
            new { heart = "murmur" },
            ApiJson.Options
        );
        var fetched = await client.GetAsync($"/vitals/{vitalsId}/child-vitals");
        var body = await fetched.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(patched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(fetched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.GetProperty("heart").GetString()).IsEqualTo("murmur");
    }

    [Test]
    public async Task Delete_ReturnsNoContent()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);
        var vitalsId = await CreateVitalsAsync(client, visitId);
        var created = await client.PostAsJsonAsync(
            "/child-vitals",
            new
            {
                vitalsId,
                heart = "normal",
                pallor = false,
            },
            ApiJson.Options
        );
        created.EnsureSuccessStatusCode();
        var id = ClinicSetup.ReadInt(
            await created.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );

        // Act
        var deleted = await client.DeleteAsync($"/child-vitals/{id}");

        // Assert
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
    }

    private static async Task<(int PatientId, int VisitId)> RegisterAsync(HttpClient client)
    {
        var suffix = ClinicSetup.Suffix();
        var villageId = await ClinicSetup.CreateVillageAsync(client, $"C{suffix}");
        return await ClinicSetup.RegisterPatientAsync(client, villageId, $"Child {suffix}");
    }

    private static async Task<int> CreateVitalsAsync(HttpClient client, int visitId)
    {
        var response = await client.PostAsJsonAsync(
            "/vitals",
            new { visitId, temperature = 36.5m },
            ApiJson.Options
        );
        response.EnsureSuccessStatusCode();
        return ClinicSetup.ReadInt(
            await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );
    }
}

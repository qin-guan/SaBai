using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Records;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class PubertyTests(AppFixture fixture)
{
    [Test]
    public async Task Create_ThenGetByVitals_ReturnsAge()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);
        var vitalsId = await CreateVitalsAsync(client, visitId);

        // Act
        var created = await client.PostAsJsonAsync(
            "/puberty",
            new
            {
                vitalsId,
                pubarche = true,
                pubarcheAge = 12,
            },
            ApiJson.Options
        );
        var fetched = await client.GetAsync($"/vitals/{vitalsId}/puberty");
        var body = await fetched.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(fetched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.GetProperty("pubarcheAge").GetInt32()).IsEqualTo(12);
    }

    [Test]
    public async Task Create_UnknownVitals_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.PostAsJsonAsync(
            "/puberty",
            new
            {
                vitalsId = 99999999,
                pubarche = true,
                pubarcheAge = 12,
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
            "/puberty",
            new
            {
                vitalsId,
                pubarche = true,
                pubarcheAge = 12,
            },
            ApiJson.Options
        );
        created.EnsureSuccessStatusCode();

        // Act
        var again = await client.PostAsJsonAsync(
            "/puberty",
            new
            {
                vitalsId,
                pubarche = true,
                pubarcheAge = 13,
            },
            ApiJson.Options
        );

        // Assert
        await Assert.That(again.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Patch_AgeWhenFlagFalse_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);
        var vitalsId = await CreateVitalsAsync(client, visitId);
        var created = await client.PostAsJsonAsync(
            "/puberty",
            new
            {
                vitalsId,
                pubarche = true,
                pubarcheAge = 12,
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
            $"/puberty/{id}",
            new { pubarche = false, pubarcheAge = 10 },
            ApiJson.Options
        );

        // Assert
        await Assert.That(patched.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
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
            "/puberty",
            new
            {
                vitalsId,
                pubarche = true,
                pubarcheAge = 12,
            },
            ApiJson.Options
        );
        created.EnsureSuccessStatusCode();
        var id = ClinicSetup.ReadInt(
            await created.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );

        // Act
        var deleted = await client.DeleteAsync($"/puberty/{id}");

        // Assert
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
    }

    private static async Task<(int PatientId, int VisitId)> RegisterAsync(HttpClient client)
    {
        var suffix = ClinicSetup.Suffix();
        var villageId = await ClinicSetup.CreateVillageAsync(client, $"P{suffix}");
        return await ClinicSetup.RegisterPatientAsync(client, villageId, $"Puberty {suffix}");
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

using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Records;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class GlassesTests(AppFixture fixture)
{
    [Test]
    public async Task Create_ThenGetByVisit_ReturnsDegreeAndNotes()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);

        // Act
        var created = await client.PostAsJsonAsync(
            "/glasses",
            new { visitId, leftGlassesDegree = "6/12", notes = "trial" },
            ApiJson.Options);
        var fetched = await client.GetAsync($"/glasses?visitId={visitId}");
        var body = await fetched.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(fetched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.GetProperty("leftGlassesDegree").GetString()).IsEqualTo("6/12");
        await Assert.That(body.GetProperty("notes").GetString()).IsEqualTo("trial");
    }

    [Test]
    public async Task Create_UnknownVisit_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.PostAsJsonAsync(
            "/glasses",
            new { visitId = 99999999, leftGlassesDegree = "6/12", notes = "trial" },
            ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetByVisit_WhenNone_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);

        // Act
        var response = await client.GetAsync($"/glasses?visitId={visitId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Patch_EmptyNotes_ClearsNotes()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);
        var created = await client.PostAsJsonAsync(
            "/glasses",
            new { visitId, leftGlassesDegree = "6/12", notes = "trial" },
            ApiJson.Options);
        created.EnsureSuccessStatusCode();
        var id = ClinicSetup.ReadInt(await created.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options), "id");

        // Act
        var patched = await client.PatchAsJsonAsync(
            $"/glasses/{id}",
            new { notes = "" },
            ApiJson.Options);
        var body = await patched.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(patched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var cleared = !body.TryGetProperty("notes", out var notes)
            || notes.ValueKind == JsonValueKind.Null
            || notes.GetString() is null or "";
        await Assert.That(cleared).IsTrue();
    }

    private static async Task<(int PatientId, int VisitId)> RegisterAsync(HttpClient client)
    {
        var suffix = ClinicSetup.Suffix();
        var villageId = await ClinicSetup.CreateVillageAsync(client, $"G{suffix}");
        return await ClinicSetup.RegisterPatientAsync(client, villageId, $"Glasses {suffix}");
    }
}

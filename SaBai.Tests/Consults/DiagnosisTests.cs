using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Consults;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class DiagnosisTests(AppFixture fixture)
{
    [Test]
    public async Task Create_ThenGet_ReturnsDetailsAndListIncludesId()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var consultId = await CreateConsultAsync(client);

        // Act
        var created = await client.PostAsJsonAsync(
            "/diagnoses",
            new { consultId, details = "wheeze", category = "Respiratory" },
            ApiJson.Options);
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var id = ClinicSetup.ReadInt(createdBody, "id");
        var fetched = await client.GetAsync($"/diagnoses/{id}");
        var body = await fetched.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var listed = await client.GetAsync($"/diagnoses?consultId={consultId}");
        var items = await listed.Content.ReadFromJsonAsync<List<JsonElement>>(ApiJson.Options);

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(fetched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.GetProperty("details").GetString()).IsEqualTo("wheeze");
        await Assert.That(body.GetProperty("category").GetString()).IsEqualTo("Respiratory");
        await Assert.That(listed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(items!.Any(x => ClinicSetup.ReadInt(x, "id") == id)).IsTrue();
    }

    [Test]
    public async Task Patch_ThenDelete_RemovesDiagnosis()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var consultId = await CreateConsultAsync(client);
        var created = await client.PostAsJsonAsync(
            "/diagnoses",
            new { consultId, details = $"open {ClinicSetup.Suffix()}", category = "Respiratory" },
            ApiJson.Options);
        created.EnsureSuccessStatusCode();
        var id = ClinicSetup.ReadInt(await created.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options), "id");

        // Act
        var patched = await client.PatchAsJsonAsync(
            $"/diagnoses/{id}",
            new { details = "resolved" },
            ApiJson.Options);
        var fetched = await client.GetAsync($"/diagnoses/{id}");
        var body = await fetched.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var deleted = await client.DeleteAsync($"/diagnoses/{id}");
        var missing = await client.GetAsync($"/diagnoses/{id}");

        // Assert
        await Assert.That(patched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(fetched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.GetProperty("details").GetString()).IsEqualTo("resolved");
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(missing.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Create_MissingConsult_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.PostAsJsonAsync(
            "/diagnoses",
            new { consultId = 99999999, details = $"absent {ClinicSetup.Suffix()}", category = "Respiratory" },
            ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Create_EmptyDetails_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var consultId = await CreateConsultAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            "/diagnoses",
            new { consultId, details = "", category = "Respiratory" },
            ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Get_Missing_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.GetAsync("/diagnoses/99999999");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Delete_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var consultId = await CreateConsultAsync(client);
        var created = await client.PostAsJsonAsync(
            "/diagnoses",
            new { consultId, details = $"locked {ClinicSetup.Suffix()}", category = "Respiratory" },
            ApiJson.Options);
        created.EnsureSuccessStatusCode();
        var id = ClinicSetup.ReadInt(await created.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options), "id");
        var anonymous = fixture.CreateHttpClient("api");

        // Act
        var response = await anonymous.DeleteAsync($"/diagnoses/{id}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    private static async Task<int> CreateConsultAsync(HttpClient client)
    {
        var suffix = ClinicSetup.Suffix();
        var villageId = await ClinicSetup.CreateVillageAsync(client, $"D{suffix}");
        var (_, visitId) = await ClinicSetup.RegisterPatientAsync(client, villageId, $"Diagnosis {suffix}");
        return await ClinicSetup.CreateConsultAsync(client, visitId, $"consult {suffix}");
    }
}

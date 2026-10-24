using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Records;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class EyesightTests(AppFixture fixture)
{
    [Test]
    public async Task Create_NormalizesDegreeAndPinhole()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);

        // Act
        var created = await client.PostAsJsonAsync(
            "/eyesight",
            new
            {
                visitId,
                leftEyeDegree = "6/9+2",
                leftEyePinhole = "ni",
            },
            ApiJson.Options
        );
        var fetched = await client.GetAsync($"/visits/{visitId}/eyesight");
        var body = await fetched.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(fetched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.GetProperty("leftEyeDegree").GetString()).IsEqualTo("6/9+2");
        await Assert.That(body.GetProperty("leftEyePinhole").GetString()).IsEqualTo("NI");
    }

    [Test]
    public async Task Create_InvalidDegree_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            "/eyesight",
            new { visitId, leftEyeDegree = "20/20" },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Create_InvalidPinhole_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            "/eyesight",
            new { visitId, leftEyePinhole = "nope" },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Create_SecondTime_ReturnsConflict()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);
        var created = await client.PostAsJsonAsync(
            "/eyesight",
            new { visitId, leftEyeDegree = "6/6" },
            ApiJson.Options
        );
        created.EnsureSuccessStatusCode();

        // Act
        var again = await client.PostAsJsonAsync(
            "/eyesight",
            new { visitId, leftEyeDegree = "6/9" },
            ApiJson.Options
        );

        // Assert
        await Assert.That(again.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Get_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.GetAsync("/eyesight/99999999");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Delete_ThenGetByVisit_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);
        var created = await client.PostAsJsonAsync(
            "/eyesight",
            new { visitId, leftEyeDegree = "CF" },
            ApiJson.Options
        );
        created.EnsureSuccessStatusCode();
        var id = ClinicSetup.ReadInt(
            await created.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );

        // Act
        var deleted = await client.DeleteAsync($"/eyesight/{id}");
        var fetched = await client.GetAsync($"/visits/{visitId}/eyesight");

        // Assert
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(fetched.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    private static async Task<(int PatientId, int VisitId)> RegisterAsync(HttpClient client)
    {
        var suffix = ClinicSetup.Suffix();
        var villageId = await ClinicSetup.CreateVillageAsync(client, $"E{suffix}");
        return await ClinicSetup.RegisterPatientAsync(client, villageId, $"Eyesight {suffix}");
    }
}

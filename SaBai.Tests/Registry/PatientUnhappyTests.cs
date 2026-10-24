using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Registry;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class PatientUnhappyTests(AppFixture fixture)
{
    [Test]
    public async Task Get_ReturnsNameAndDisplayId()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var code = $"V{suffix}";
        var name = $"Patient {suffix}";
        var villageId = await ClinicSetup.CreateVillageAsync(client, code);
        var (patientId, _) = await ClinicSetup.RegisterPatientAsync(client, villageId, name);

        // Act
        var response = await client.GetAsync($"/patients/{patientId}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.GetProperty("name").GetString()).IsEqualTo(name);
        await Assert.That(body.GetProperty("displayId").GetString()!.StartsWith(code)).IsTrue();
    }

    [Test]
    public async Task Create_MissingVillage_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.PostAsJsonAsync(
            "/patients",
            new
            {
                name = $"Patient {ClinicSetup.Suffix()}",
                gender = "female",
                drugAllergy = "None",
                villageCodeId = 99999999,
                dateOfBirth = new DateTimeOffset(1991, 3, 4, 0, 0, 0, TimeSpan.Zero),
            },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Create_OmittingGender_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var villageId = await ClinicSetup.CreateVillageAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            "/patients",
            new
            {
                name = $"Patient {ClinicSetup.Suffix()}",
                drugAllergy = "None",
                villageCodeId = villageId,
                dateOfBirth = new DateTimeOffset(1991, 3, 4, 0, 0, 0, TimeSpan.Zero),
            },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Get_MissingPatient_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.GetAsync("/patients/99999999");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Patch_MissingPatient_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.PatchAsJsonAsync(
            "/patients/99999999",
            new { name = "Missing" },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task List_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");

        // Act
        var response = await client.GetAsync("/patients");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}

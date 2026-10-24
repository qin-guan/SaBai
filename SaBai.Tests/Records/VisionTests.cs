using System.Net.Http.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Records;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class VisionTests(AppFixture fixture)
{
    [Test]
    public async Task Get_AfterGlasses_ContainsPrescribedDegree()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);
        const string degree = "6/12";
        var created = await client.PostAsJsonAsync(
            "/glasses",
            new
            {
                visitId,
                leftGlassesDegree = degree,
                notes = "trial",
            },
            ApiJson.Options
        );
        created.EnsureSuccessStatusCode();

        // Act
        var vision = await client.GetAsync($"/vision?visitId={visitId}");
        var body = await vision.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(vision.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.Contains(degree)).IsTrue();
    }

    [Test]
    public async Task Get_WithoutVisitId_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.GetAsync("/vision");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Get_VisitIdZero_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.GetAsync("/vision?visitId=0");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Get_UnknownVisit_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.GetAsync("/vision?visitId=99999999");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Get_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");

        // Act
        var response = await client.GetAsync("/vision?visitId=1");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    private static async Task<(int PatientId, int VisitId)> RegisterAsync(HttpClient client)
    {
        var suffix = ClinicSetup.Suffix();
        var villageId = await ClinicSetup.CreateVillageAsync(client, $"N{suffix}");
        return await ClinicSetup.RegisterPatientAsync(client, villageId, $"Vision {suffix}");
    }
}

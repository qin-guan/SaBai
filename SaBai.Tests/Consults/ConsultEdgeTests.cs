using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Consults;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class ConsultEdgeTests(AppFixture fixture)
{
    [Test]
    public async Task Create_MissingVisit_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var details = $"missing visit {ClinicSetup.Suffix()}";

        // Act
        var response = await client.PostAsJsonAsync(
            "/consults",
            new
            {
                visitId = 99999999,
                diagnoses = new[] { new { details, category = "Respiratory" } },
            },
            ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Create_EmptyDiagnoses_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            "/consults",
            new { visitId, diagnoses = Array.Empty<object>() },
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
        var response = await client.GetAsync("/consults/99999999");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Patch_Missing_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.PatchAsJsonAsync(
            "/consults/99999999",
            new { consultation = "absent" },
            ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Delete_Missing_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.DeleteAsync("/consults/99999999");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Delete_WhenReferralExists_ReturnsConflict()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var (_, visitId) = await RegisterAsync(client);
        var consultId = await ClinicSetup.CreateConsultAsync(client, visitId, $"blocked {suffix}");
        var referral = await client.PostAsJsonAsync(
            "/referrals",
            new { consultId, referredFor = $"hold {suffix}" },
            ApiJson.Options);
        referral.EnsureSuccessStatusCode();

        // Act
        var response = await client.DeleteAsync($"/consults/{consultId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task GetPdf_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);
        var consultId = await ClinicSetup.CreateConsultAsync(client, visitId, $"pdf {ClinicSetup.Suffix()}");
        var anonymous = fixture.CreateHttpClient("api");

        // Act
        var response = await anonymous.GetAsync($"/consults/{consultId}/pdf");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Get_ReturnsDoctorNicknameAndDiagnosis()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var details = $"dry cough {ClinicSetup.Suffix()}";
        var (_, visitId) = await RegisterAsync(client);
        var consultId = await ClinicSetup.CreateConsultAsync(client, visitId, details);

        // Act
        var response = await client.GetAsync($"/consults/{consultId}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.GetProperty("doctorNickname").GetString()).IsEqualTo("Admin");
        await Assert.That(body.GetProperty("diagnoses").EnumerateArray().Any(x => x.GetProperty("details").GetString() == details)).IsTrue();
    }

    private static async Task<(int PatientId, int VisitId)> RegisterAsync(HttpClient client)
    {
        var suffix = ClinicSetup.Suffix();
        var villageId = await ClinicSetup.CreateVillageAsync(client, $"C{suffix}");
        return await ClinicSetup.RegisterPatientAsync(client, villageId, $"Consult {suffix}");
    }
}

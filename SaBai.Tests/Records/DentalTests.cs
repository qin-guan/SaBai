using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Records;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class DentalTests(AppFixture fixture)
{
    [Test]
    public async Task Put_CreatesThenReturnsConsultation()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);

        // Act
        var saved = await client.PutAsJsonAsync(
            $"/visits/{visitId}/dental",
            new
            {
                previousDentalVisit = "Unknown",
                toothache = true,
                brushingFrequency = "ThreeOrMore",
                chiefComplaint = "  Pain on lower left  ",
                facialSymmetry = "Asymmetrical",
                lymphadenopathy = "NotAssessed",
                plaque = "Heavy",
                caries = "NonCavitated",
                malocclusion = "Suspected",
                diagnoses = new[] { "Dental caries", "  ", "Gingivitis" },
                fluorideDone = true,
                fluorideDate = "2026-11-02",
                fluorideOperator = "Dr Tan",
                fluorideToothNumbers = "36, 46",
                referredFor = "Restorative",
                referralPriority = "Early",
            },
            ApiJson.Options
        );
        var fetched = await client.GetAsync($"/visits/{visitId}/dental");
        var body = await fetched.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(saved.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(fetched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.GetProperty("previousDentalVisit").GetString()).IsEqualTo("Unknown");
        await Assert.That(body.GetProperty("toothache").GetBoolean()).IsTrue();
        await Assert
            .That(body.GetProperty("brushingFrequency").GetString())
            .IsEqualTo("ThreeOrMore");
        await Assert
            .That(body.GetProperty("chiefComplaint").GetString())
            .IsEqualTo("Pain on lower left");
        await Assert.That(body.GetProperty("caries").GetString()).IsEqualTo("NonCavitated");
        await Assert.That(body.GetProperty("diagnoses").GetArrayLength()).IsEqualTo(2);
        await Assert.That(body.GetProperty("fluorideDate").GetString()).IsEqualTo("2026-11-02");
        await Assert.That(body.GetProperty("referralPriority").GetString()).IsEqualTo("Early");
    }

    [Test]
    public async Task Put_SecondTime_ReplacesAndClearsOmittedFields()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);
        var first = await client.PutAsJsonAsync(
            $"/visits/{visitId}/dental",
            new { oralHygiene = "Poor", remarks = "First" },
            ApiJson.Options
        );
        first.EnsureSuccessStatusCode();
        var firstId = ClinicSetup.ReadInt(
            await first.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );

        // Act
        var second = await client.PutAsJsonAsync(
            $"/visits/{visitId}/dental",
            new { oralHygiene = "Good" },
            ApiJson.Options
        );
        var body = await second.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(ClinicSetup.ReadInt(body, "id")).IsEqualTo(firstId);
        await Assert.That(body.GetProperty("oralHygiene").GetString()).IsEqualTo("Good");
        await Assert.That(IsNullOrMissing(body, "remarks")).IsTrue();
    }

    [Test]
    public async Task Put_UnknownOption_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);

        // Act
        var response = await client.PutAsJsonAsync(
            $"/visits/{visitId}/dental",
            new { plaque = "Extreme" },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Put_MissingVisit_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.PutAsJsonAsync(
            "/visits/999999999/dental",
            new { oralHygiene = "Good" },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Get_NoConsultation_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var (_, visitId) = await RegisterAsync(client);

        // Act
        var response = await client.GetAsync($"/visits/{visitId}/dental");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    private static bool IsNullOrMissing(JsonElement body, string name) =>
        !body.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null;

    private static async Task<(int PatientId, int VisitId)> RegisterAsync(HttpClient client)
    {
        var suffix = ClinicSetup.Suffix();
        var villageId = await ClinicSetup.CreateVillageAsync(client, $"D{suffix}");
        return await ClinicSetup.RegisterPatientAsync(client, villageId, $"Dental {suffix}");
    }
}

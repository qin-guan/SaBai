using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Consults;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class ChartTests(AppFixture fixture)
{
    [Test]
    public async Task PatientRecords_AfterConsult_ContainsPatientAndConsult()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var patientName = $"Records {suffix}";
        var villageId = await ClinicSetup.CreateVillageAsync(client, $"H{suffix}");
        var (_, visitId) = await ClinicSetup.RegisterPatientAsync(client, villageId, patientName);
        var consultId = await ClinicSetup.CreateConsultAsync(
            client,
            visitId,
            $"chart wheeze {suffix}"
        );

        // Act
        var response = await client.GetAsync($"/patient-records?visitId={visitId}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert
            .That(body.GetProperty("patient").GetProperty("name").GetString())
            .IsEqualTo(patientName);
        await Assert
            .That(
                body.GetProperty("consults")
                    .EnumerateArray()
                    .Any(x => x.GetProperty("id").GetInt32() == consultId)
            )
            .IsTrue();
    }

    [Test]
    public async Task PatientConsult_ContainsDoctorNickname()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var villageId = await ClinicSetup.CreateVillageAsync(client, $"J{suffix}");
        var (_, visitId) = await ClinicSetup.RegisterPatientAsync(
            client,
            villageId,
            $"Workspace {suffix}"
        );
        await ClinicSetup.CreateConsultAsync(client, visitId, $"workspace {suffix}");

        // Act
        var response = await client.GetAsync($"/patient-consult?visitId={visitId}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert
            .That(
                body.GetProperty("consults")
                    .EnumerateArray()
                    .Any(x => x.GetProperty("doctorNickname").GetString() == "Admin")
            )
            .IsTrue();
    }

    [Test]
    public async Task PatientRecords_VisitIdZero_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.GetAsync("/patient-records?visitId=0");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task PatientConsult_MissingVisit_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.GetAsync("/patient-consult?visitId=99999999");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task PatientRecords_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");

        // Act
        var response = await client.GetAsync("/patient-records");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}

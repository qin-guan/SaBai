using System.Net;
using System.Net.Http.Json;
using SaBai.API.Domain;
using SaBai.Tests.Support;
using PatientVisitsResponse = SaBai.API.Endpoints.Patients.Visits.Get.Response;
using VisitListResponse = SaBai.API.Endpoints.Visits.List.Get.Response;

namespace SaBai.Tests.Patients;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class VisitTests(AppFixture fixture)
{
    [Test]
    public async Task List_IncludesVillage()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var code = ClinicSamples.Code();
        var village = await ClinicSamples.CreateVillageAsync(client, code, colorHex: "#778899", name: $"Seen {code}");
        var patient = await ClinicSamples.RegisterAsync(client, village.Id, $"Visited {code}");

        // Act
        var listed = await client.GetFromJsonAsync<VisitListResponse>($"/visits?patientId={patient.Id}", ApiJson.Options);
        var byPatient = await client.GetFromJsonAsync<PatientVisitsResponse>($"/patients/{patient.Id}/visits", ApiJson.Options);
        var visit = listed!.Visits.Single(x => x.Id == patient.VisitId);
        var nested = byPatient!.Visits.Single(x => x.Id == patient.VisitId);

        // Assert
        await Assert.That(visit.PatientName).IsEqualTo(patient.Name);
        await Assert.That(visit.VillageCode).IsEqualTo(code);
        await Assert.That(visit.VillageName).IsEqualTo(village.Name);
        await Assert.That(visit.VillageColorHex).IsEqualTo("#778899");
        await Assert.That(visit.Status).IsEqualTo(Visit.StartedStatus);
        await Assert.That(nested.VillageCode).IsEqualTo(code);
        await Assert.That(nested.VillageName).IsEqualTo(village.Name);
        await Assert.That(nested.VillageColorHex).IsEqualTo("#778899");
    }

    [Test]
    public async Task Delete_WithoutConsult_ReturnsNoContent()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var code = ClinicSamples.Code();
        var village = await ClinicSamples.CreateVillageAsync(client, code);
        var patient = await ClinicSamples.RegisterAsync(client, village.Id, $"Closable {code}", withVitals: true);

        // Act
        var deleted = await client.DeleteAsync($"/visits/{patient.VisitId}");
        var missing = await client.GetAsync($"/visits/{patient.VisitId}");

        // Assert
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(missing.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}

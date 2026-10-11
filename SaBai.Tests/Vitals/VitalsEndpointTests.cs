using System.Net.Http.Json;
using System.Text.Json;
using SaBai.API.Domain;
using SaBai.Tests.Support;

namespace SaBai.Tests.Vitals;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class VitalsEndpointTests(AppFixture fixture)
{
    [Test]
    public async Task CreateVitals_SecondCreate_ReturnsConflict()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var seed = await SeedVisitAsync(client);

        // Act
        var created = await client.PostAsJsonAsync(
            "/vitals",
            new { visitId = seed.VisitId, height = 160m, weight = 50m },
            ApiJson.Options);
        var again = await client.PostAsJsonAsync(
            "/vitals",
            new { visitId = seed.VisitId, height = 161m },
            ApiJson.Options);
        var body = await created.Content.ReadFromJsonAsync<VitalsBody>(ApiJson.Options);

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(created.Headers.Location).IsNotNull();
        await Assert.That(body!.VisitId).IsEqualTo(seed.VisitId);
        await Assert.That(body.Height).IsEqualTo(160m);
        await Assert.That(again.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task GetVitals_ByVisit_ReturnsRecord()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var seed = await SeedVisitAsync(client);
        var created = await client.PostAsJsonAsync(
            "/vitals",
            new { visitId = seed.VisitId, heartRate = 72, urineTest = "clear" },
            ApiJson.Options);
        var saved = await created.Content.ReadFromJsonAsync<VitalsBody>(ApiJson.Options);

        // Act
        var byVisit = await client.GetAsync($"/visits/{seed.VisitId}/vitals");
        var body = await byVisit.Content.ReadFromJsonAsync<VitalsBody>(ApiJson.Options);
        var listed = await client.GetAsync($"/patients/{seed.PatientId}/vitals");
        var list = await listed.Content.ReadFromJsonAsync<List<VitalsBody>>(ApiJson.Options);

        // Assert
        await Assert.That(byVisit.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body!.Id).IsEqualTo(saved!.Id);
        await Assert.That(body.VisitId).IsEqualTo(seed.VisitId);
        await Assert.That(body.HeartRate).IsEqualTo(72);
        await Assert.That(body.UrineTest).IsEqualTo("clear");
        await Assert.That(listed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(list!.Count).IsEqualTo(1);
        await Assert.That(list[0].Id).IsEqualTo(saved.Id);
    }

    [Test]
    public async Task PatchVitals_UpdatesProvidedFields()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var seed = await SeedVisitAsync(client);
        var created = await client.PostAsJsonAsync(
            "/vitals",
            new { visitId = seed.VisitId, height = 150m, weight = 40m },
            ApiJson.Options);
        var saved = await created.Content.ReadFromJsonAsync<VitalsBody>(ApiJson.Options);

        // Act
        var patched = await client.PatchAsJsonAsync(
            $"/vitals/{saved!.Id}",
            new { height = 151m },
            ApiJson.Options);
        var body = await patched.Content.ReadFromJsonAsync<VitalsBody>(ApiJson.Options);

        // Assert
        await Assert.That(patched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body!.Height).IsEqualTo(151m);
        await Assert.That(body.Weight).IsEqualTo(40m);
        await Assert.That(body.VisitId).IsEqualTo(seed.VisitId);
    }

    [Test]
    public async Task DeleteVitals_ReturnsNoContent()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var seed = await SeedVisitAsync(client);
        var created = await client.PostAsJsonAsync(
            "/vitals",
            new { visitId = seed.VisitId, temperature = 36.6m },
            ApiJson.Options);
        var saved = await created.Content.ReadFromJsonAsync<VitalsBody>(ApiJson.Options);

        // Act
        var deleted = await client.DeleteAsync($"/vitals/{saved!.Id}");
        var fetched = await client.GetAsync($"/vitals/{saved.Id}");

        // Assert
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(fetched.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CreateEyesight_InvalidAcuity_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var seed = await SeedVisitAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            "/eyesight",
            new { visitId = seed.VisitId, leftEyeDegree = "20/20" },
            ApiJson.Options);
        var detail = await response.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(detail).Contains("VA notation");
    }

    [Test]
    public async Task CreateEyesight_ValidAcuity_StoresUppercased()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var seed = await SeedVisitAsync(client);

        // Act
        var created = await client.PostAsJsonAsync(
            "/eyesight",
            new
            {
                visitId = seed.VisitId,
                leftEyeDegree = "6/9+2",
                rightEyeDegree = "cf",
                leftEyePinhole = "ni",
            },
            ApiJson.Options);
        var fetched = await client.GetAsync($"/visits/{seed.VisitId}/eyesight");
        var body = await fetched.Content.ReadFromJsonAsync<EyesightBody>(ApiJson.Options);

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(fetched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body!.LeftEyeDegree).IsEqualTo("6/9+2");
        await Assert.That(body.RightEyeDegree).IsEqualTo("CF");
        await Assert.That(body.LeftEyePinhole).IsEqualTo("NI");
    }

    [Test]
    public async Task CreatePuberty_AgeWhenFlagFalse_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var seed = await SeedVisitAsync(client);
        var vitals = await client.PostAsJsonAsync("/vitals", new { visitId = seed.VisitId }, ApiJson.Options);
        var saved = await vitals.Content.ReadFromJsonAsync<VitalsBody>(ApiJson.Options);

        // Act
        var denied = await client.PostAsJsonAsync(
            "/puberty",
            new { vitalsId = saved!.Id, pubarche = false, pubarcheAge = 12 },
            ApiJson.Options);
        var detail = await denied.Content.ReadAsStringAsync();
        var created = await client.PostAsJsonAsync(
            "/puberty",
            new { vitalsId = saved.Id, pubarche = true, pubarcheAge = 12 },
            ApiJson.Options);

        // Assert
        await Assert.That(denied.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(detail).Contains("pubarcheAge must not be set when pubarche is false");
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateGlasses_VisionComposite_ReturnsDegrees()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var seed = await SeedVisitAsync(client);

        // Act
        var created = await client.PostAsJsonAsync(
            "/glasses",
            new
            {
                visitId = seed.VisitId,
                leftGlassesDegree = "-1.25",
                rightGlassesDegree = "-0.75",
                notes = "distance",
            },
            ApiJson.Options);
        var vision = await client.GetAsync($"/vision?visitId={seed.VisitId}");
        var body = await vision.Content.ReadFromJsonAsync<VisionBody>(ApiJson.Options);

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(vision.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body!.Patient.Id).IsEqualTo(seed.PatientId);
        await Assert.That(body.Patient.Name).IsEqualTo(seed.PatientName);
        await Assert.That(body.Patient.DisplayId).IsEqualTo($"{seed.VillageCode}{seed.PatientId:0000}");
        await Assert.That(body.Patient.Gender).IsEqualTo(Gender.Male);
        await Assert.That(body.Vision).IsNotNull();
        await Assert.That(body.Vision!.VisitId).IsEqualTo(seed.VisitId);
        await Assert.That(body.Vision.LeftGlassesDegree).IsEqualTo("-1.25");
        await Assert.That(body.Vision.RightGlassesDegree).IsEqualTo("-0.75");
        await Assert.That(body.Vision.Notes).IsEqualTo("distance");
    }

    private static async Task<Seed> SeedVisitAsync(HttpClient client)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var code = $"V{suffix[..12]}";
        var name = $"Patient {suffix[..8]}";
        var village = await client.PostAsJsonAsync(
            "/village-codes",
            new
            {
                code,
                name = $"Village {suffix[..8]}",
                colorHex = "#336699",
                isVisible = true,
            },
            ApiJson.Options);
        var villageBody = await ReadCreatedAsync(village, "Create village code");
        var patient = await client.PostAsJsonAsync(
            "/patients",
            new
            {
                name,
                gender = "male",
                dateOfBirth = new DateTimeOffset(2014, 3, 2, 0, 0, 0, TimeSpan.Zero),
                drugAllergy = "NKDA",
                hasPoorCard = false,
                hasBS2Card = false,
                hasSabaiCard = false,
                villageCodeId = villageBody.GetProperty("id").GetInt32(),
            },
            ApiJson.Options);
        var patientBody = await ReadCreatedAsync(patient, "Create patient");
        return new Seed(
            patientBody.GetProperty("id").GetInt32(),
            patientBody.GetProperty("visitId").GetInt32(),
            code,
            name);
    }

    private static async Task<JsonElement> ReadCreatedAsync(HttpResponseMessage response, string action)
    {
        if (response.StatusCode != HttpStatusCode.Created)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"{action} returned {(int)response.StatusCode}: {error}");
        }

        return (await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options))!;
    }

    private sealed record Seed(int PatientId, int VisitId, string VillageCode, string PatientName);

    private sealed record VitalsBody(
        int Id,
        int VisitId,
        decimal? Height,
        decimal? Weight,
        decimal? Temperature,
        int? HeartRate,
        string? UrineTest);

    private sealed record EyesightBody(
        int Id,
        int VisitId,
        string? LeftEyeDegree,
        string? RightEyeDegree,
        string? LeftEyePinhole);

    private sealed record VisionBody(VisionPatient Patient, GlassesBody? Vision, AcuityBody? Vitals);

    private sealed record VisionPatient(
        int Id,
        string Name,
        string DisplayId,
        Gender Gender,
        DateTimeOffset DateOfBirth);

    private sealed record GlassesBody(
        int Id,
        int VisitId,
        string? LeftGlassesDegree,
        string? RightGlassesDegree,
        string? Notes);

    private sealed record AcuityBody(
        string? LeftEyeDegree,
        string? RightEyeDegree,
        string? LeftEyePinhole,
        string? RightEyePinhole);
}

using System.Net;
using System.Net.Http.Json;
using SaBai.API.Domain;
using SaBai.Tests.Support;
using LatestVisitResponse = SaBai.API.Endpoints.Patients.WithLatestVisit.Get.Response;
using PatientListResponse = SaBai.API.Endpoints.Patients.List.Get.Response;
using PatientResponse = SaBai.API.Endpoints.Patients.Get.Response;
using VisitResponse = SaBai.API.Endpoints.Visits.Get.Response;

namespace SaBai.Tests.Patients;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class PatientTests(AppFixture fixture)
{
    [Test]
    public async Task Create_PersistsPatientVisitAndVitals()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var code = ClinicSamples.Code();
        var village = await ClinicSamples.CreateVillageAsync(
            client,
            code,
            colorHex: "#102030",
            name: $"Reg {code}"
        );

        // Act
        var created = await client.PostAsJsonAsync(
            "/patients",
            new
            {
                name = "Siti Aminah",
                identificationNumber = $"NID-{code}",
                contactNo = "011223344",
                gender = Gender.Female,
                dateOfBirth = new DateTimeOffset(1990, 5, 1, 0, 0, 0, TimeSpan.Zero),
                drugAllergy = "None",
                hasPoorCard = true,
                hasBS2Card = false,
                hasSabaiCard = true,
                toGetReport = true,
                villageCodeId = village.Id,
                vitals = new
                {
                    temperature = 36.6m,
                    height = 160m,
                    weight = 55m,
                    systolic = 120,
                    diastolic = 80,
                    heartRate = 72,
                },
            },
            ApiJson.Options
        );
        var body =
            await created.Content.ReadFromJsonAsync<SaBai.API.Endpoints.Patients.Create.Post.Response>(
                ApiJson.Options
            );
        var visit = await client.GetFromJsonAsync<VisitResponse>(
            $"/visits/{body!.VisitId}",
            ApiJson.Options
        );
        var patient = await client.GetFromJsonAsync<PatientResponse>(
            $"/patients/{body.Id}",
            ApiJson.Options
        );

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(created.Headers.Location).IsNotNull();
        await Assert.That(created.Headers.Location!.ToString()).Contains($"/patients/{body.Id}");
        await Assert.That(body.DisplayId).IsEqualTo($"{code}{body.Id:0000}");
        await Assert.That(body.FilterString).Contains("Siti Aminah");
        await Assert.That(body.VillageCode).IsEqualTo(code);
        await Assert.That(body.Gender).IsEqualTo(Gender.Female);
        await Assert.That(body.DrugAllergy).IsEqualTo("None");
        await Assert.That(body.VisitId > 0).IsTrue();
        await Assert.That(body.VitalsId).IsNotNull();
        await Assert.That(body.LastVisitId).IsEqualTo(body.VisitId);
        await Assert.That(visit!.Status).IsEqualTo(Visit.StartedStatus);
        await Assert.That(visit.PatientName).IsEqualTo("Siti Aminah");
        await Assert.That(visit.VillageCode).IsEqualTo(code);
        await Assert.That(visit.VillageColorHex).IsEqualTo("#102030");
        await Assert.That(patient!.DisplayId).IsEqualTo(body.DisplayId);
        await Assert.That(patient.HasPoorCard).IsTrue();
        await Assert.That(patient.HasSabaiCard).IsTrue();
    }

    [Test]
    public async Task List_FiltersNameAndVillageCodeIgnoringCase()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var code = ClinicSamples.Code().ToUpperInvariant();
        var village = await ClinicSamples.CreateVillageAsync(client, code);
        var name = $"Filter {code}";
        var patient = await ClinicSamples.RegisterAsync(client, village.Id, name);

        // Act
        var matched = await client.GetFromJsonAsync<PatientListResponse>(
            $"/patients?name={Uri.EscapeDataString(name.ToLowerInvariant())}&villageCode={Uri.EscapeDataString(code.ToLowerInvariant())}",
            ApiJson.Options
        );
        var missed = await client.GetFromJsonAsync<PatientListResponse>(
            $"/patients?name=zz{Guid.NewGuid():N}",
            ApiJson.Options
        );

        // Assert
        await Assert
            .That(
                matched!.Patients.Any(x =>
                    x.Id == patient.Id && x.DisplayId == $"{code}{patient.Id:0000}"
                )
            )
            .IsTrue();
        await Assert.That(missed!.Patients.Any(x => x.Id == patient.Id)).IsFalse();
    }

    [Test]
    public async Task WithLatestVisit_PutsVisitedPatientsBeforeVisitLessOnes()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var code = ClinicSamples.Code();
        var village = await ClinicSamples.CreateVillageAsync(client, code, colorHex: "#ABCDEF");
        var recentName = $"Recent {code}";
        var olderName = $"Older {code}";
        var noneName = $"None {code}";
        var recent = await ClinicSamples.RegisterAsync(client, village.Id, recentName);
        var older = await ClinicSamples.RegisterAsync(client, village.Id, olderName);
        var none = await ClinicSamples.RegisterAsync(client, village.Id, noneName);
        var aged = await client.PatchAsJsonAsync(
            $"/visits/{older.VisitId}",
            new { date = DateTimeOffset.UtcNow.AddDays(-2) },
            ApiJson.Options
        );
        var removed = await client.DeleteAsync($"/visits/{none.VisitId}");

        // Act
        var listed = await client.GetFromJsonAsync<LatestVisitResponse>(
            "/patients/with-latest-visit",
            ApiJson.Options
        );
        var names = listed!.Patients.Select(x => x.Name).ToList();
        var recentRow = listed.Patients.Single(x => x.Id == recent.Id);

        // Assert
        await Assert.That(aged.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(removed.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(names.IndexOf(recentName) < names.IndexOf(olderName)).IsTrue();
        await Assert.That(names.IndexOf(olderName) < names.IndexOf(noneName)).IsTrue();
        await Assert.That(recentRow.VillageColorHex).IsEqualTo("#ABCDEF");
        await Assert.That(recentRow.LastVisitId).IsEqualTo(recent.VisitId);
    }

    [Test]
    public async Task Patch_DuplicateIdentificationNumber_ReturnsConflict()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var code = ClinicSamples.Code();
        var village = await ClinicSamples.CreateVillageAsync(client, code);
        var identificationNumber = $"NID-{Guid.NewGuid():N}";
        await ClinicSamples.RegisterAsync(
            client,
            village.Id,
            $"First {code}",
            identificationNumber
        );
        var second = await ClinicSamples.RegisterAsync(
            client,
            village.Id,
            $"Second {code}",
            $"NID-{Guid.NewGuid():N}"
        );

        // Act
        var conflict = await client.PatchAsJsonAsync(
            $"/patients/{second.Id}",
            new { identificationNumber = $"  {identificationNumber}  " },
            ApiJson.Options
        );
        var problem = await conflict.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(conflict.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert
            .That(problem)
            .Contains("Another patient already has this identification number.");
    }

    [Test]
    public async Task Delete_WhenVisitExists_ReturnsConflict()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var code = ClinicSamples.Code();
        var village = await ClinicSamples.CreateVillageAsync(client, code);
        var patient = await ClinicSamples.RegisterAsync(client, village.Id, $"Kept {code}");

        // Act
        var deleted = await client.DeleteAsync($"/patients/{patient.Id}");

        // Assert
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Get_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");

        // Act
        var response = await client.GetAsync("/patients");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}

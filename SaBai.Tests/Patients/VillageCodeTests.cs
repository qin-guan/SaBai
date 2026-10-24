using System.Net;
using System.Net.Http.Json;
using SaBai.Tests.Support;
using VillageListResponse = SaBai.API.Endpoints.VillageCodes.List.Get.Response;

namespace SaBai.Tests.Patients;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class VillageCodeTests(AppFixture fixture)
{
    [Test]
    public async Task Create_PersistsVillageAndRejectsDuplicateCode()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var code = ClinicSamples.Code();

        // Act
        var created = await client.PostAsJsonAsync(
            "/village-codes",
            new
            {
                code,
                name = $"Village {code}",
                colorHex = "#A1B2C3",
                isVisible = true,
            },
            ApiJson.Options
        );
        var village =
            await created.Content.ReadFromJsonAsync<SaBai.API.Endpoints.VillageCodes.Create.Post.Response>(
                ApiJson.Options
            );
        var duplicate = await client.PostAsJsonAsync(
            "/village-codes",
            new
            {
                code,
                name = "Other",
                colorHex = "#111111",
            },
            ApiJson.Options
        );

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(created.Headers.Location).IsNotNull();
        await Assert
            .That(created.Headers.Location!.ToString())
            .Contains($"/village-codes/{village!.Id}");
        await Assert.That(village.Code).IsEqualTo(code);
        await Assert.That(village.ColorHex).IsEqualTo("#A1B2C3");
        await Assert.That(village.IsVisible).IsTrue();
        await Assert.That(duplicate.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task List_HidesInvisibleCodesUnlessRequested()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var visibleCode = ClinicSamples.Code();
        var hiddenCode = ClinicSamples.Code();
        await ClinicSamples.CreateVillageAsync(client, visibleCode);
        await ClinicSamples.CreateVillageAsync(
            client,
            hiddenCode,
            isVisible: false,
            colorHex: "#ABC"
        );

        // Act
        var visibleOnly = await client.GetFromJsonAsync<VillageListResponse>(
            "/village-codes",
            ApiJson.Options
        );
        var includingHidden = await client.GetFromJsonAsync<VillageListResponse>(
            "/village-codes?includeHidden=true",
            ApiJson.Options
        );

        // Assert
        await Assert.That(visibleOnly!.VillageCodes.Any(x => x.Code == visibleCode)).IsTrue();
        await Assert.That(visibleOnly.VillageCodes.Any(x => x.Code == hiddenCode)).IsFalse();
        await Assert
            .That(
                includingHidden!.VillageCodes.Any(x => x.Code == hiddenCode && x.ColorHex == "#ABC")
            )
            .IsTrue();
    }

    [Test]
    public async Task Delete_WhenVisitExists_ReturnsConflict()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var code = ClinicSamples.Code();
        var village = await ClinicSamples.CreateVillageAsync(
            client,
            code,
            colorHex: "#445566",
            name: $"Blocked {code}"
        );
        await ClinicSamples.RegisterAsync(client, village.Id, $"Patient {code}");

        // Act
        var deleted = await client.DeleteAsync($"/village-codes/{village.Id}");
        var stillThere = await client.GetAsync($"/village-codes/{village.Id}");

        // Assert
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(stillThere.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}

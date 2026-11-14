using System.Net.Http.Json;
using System.Text.Json;
using SaBai.API.Domain;
using SaBai.Tests.Support;

namespace SaBai.Tests.Patients;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class PatientTagTests(AppFixture fixture)
{
    [Test]
    public async Task Patient_SupportsMultipleFreeformTags_AcrossCreateUpdateAndQueries()
    {
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var village = await ClinicSamples.CreateVillageAsync(client, ClinicSamples.Code());

        var createRes = await client.PostAsJsonAsync(
            "/patients",
            new
            {
                name = $"Tagged Patient {ClinicSamples.Code()}",
                contactNo = "012345678",
                gender = Gender.Female,
                dateOfBirth = new DateTimeOffset(1992, 3, 15, 0, 0, 0, TimeSpan.Zero),
                drugAllergy = "None",
                hasPoorCard = false,
                hasBS2Card = false,
                hasSabaiCard = true,
                toGetReport = false,
                villageCodeId = village.Id,
                tags = new[] { "Follow-Up", "  Mobility-Aid  ", "follow-up" },
            },
            ApiJson.Options
        );
        await Assert.That(createRes.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var patientId = created.GetProperty("id").GetGuid();
        var createdTags = created
            .GetProperty("tags")
            .EnumerateArray()
            .Select(x => x.GetString())
            .ToList();
        await Assert.That(createdTags.Count).IsEqualTo(2);
        await Assert.That(createdTags).Contains("Follow-Up");
        await Assert.That(createdTags).Contains("Mobility-Aid");

        var listRes = await client.GetAsync("/patients?tag=mobility-aid");
        await Assert.That(listRes.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var listBody = await listRes.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var listedIds = listBody
            .GetProperty("patients")
            .EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid())
            .ToList();
        await Assert.That(listedIds).Contains(patientId);

        var patchRes = await client.PatchAsJsonAsync(
            $"/patients/{patientId}",
            new { tags = new[] { "Chronic-Care", "VIP" } },
            ApiJson.Options
        );
        await Assert.That(patchRes.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var patched = await patchRes.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var patchedTags = patched
            .GetProperty("tags")
            .EnumerateArray()
            .Select(x => x.GetString())
            .ToList();
        await Assert.That(patchedTags.Count).IsEqualTo(2);
        await Assert.That(patchedTags).Contains("Chronic-Care");
        await Assert.That(patchedTags).Contains("VIP");

        var getRes = await client.GetAsync($"/patients/{patientId}");
        await Assert.That(getRes.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var got = await getRes.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var gotTags = got
            .GetProperty("tags")
            .EnumerateArray()
            .Select(x => x.GetString())
            .ToList();
        await Assert.That(gotTags).Contains("Chronic-Care");
        await Assert.That(gotTags).Contains("VIP");
    }
}

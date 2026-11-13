using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Consults;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class ReferralPartnerTests(AppFixture fixture)
{
    [Test]
    public async Task Create_List_Update_Delete_RoundTrip()
    {
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();

        var created = await client.PostAsJsonAsync(
            "/referral-partners",
            new { name = $"Clinic {suffix}", remarks = "Eye care", isActive = true },
            ApiJson.Options
        );
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var id = createdBody.GetProperty("id").GetGuid();

        var listed = await client.GetAsync("/referral-partners");
        var list = await listed.Content.ReadFromJsonAsync<List<JsonElement>>(ApiJson.Options);

        var updated = await client.PutAsJsonAsync(
            $"/referral-partners/{id}",
            new { id, name = $"Updated {suffix}", remarks = "", isActive = false },
            ApiJson.Options
        );
        var updatedBody = await updated.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        var deleted = await client.DeleteAsync($"/referral-partners/{id}");

        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(listed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(list!.Any(x => x.GetProperty("id").GetGuid() == id)).IsTrue();
        await Assert.That(updated.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(updatedBody.GetProperty("name").GetString()).IsEqualTo($"Updated {suffix}");
        await Assert.That(updatedBody.GetProperty("isActive").GetBoolean()).IsFalse();
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task Delete_WhenReferencedByReferral_ReturnsConflict()
    {
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();

        var villageId = await ClinicSetup.CreateVillageAsync(client, $"P{suffix}");
        var (_, visitId) = await ClinicSetup.RegisterPatientAsync(client, villageId, $"PartnerRef {suffix}");
        var consultId = await ClinicSetup.CreateConsultAsync(client, visitId, $"refer {suffix}");

        var referralRes = await client.PostAsJsonAsync(
            "/referrals",
            new { consultId, referredFor = $"dental {suffix}" },
            ApiJson.Options
        );
        referralRes.EnsureSuccessStatusCode();
        var referralId = ClinicSetup.ReadInt(
            await referralRes.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );

        var partnerRes = await client.PostAsJsonAsync(
            "/referral-partners",
            new { name = $"Locked {suffix}", isActive = true },
            ApiJson.Options
        );
        partnerRes.EnsureSuccessStatusCode();
        var partnerId = (await partnerRes.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options))
            .GetProperty("id")
            .GetGuid();

        var linkRes = await client.PatchAsJsonAsync(
            $"/referrals/{referralId}/booth",
            new { id = referralId, referredToId = partnerId },
            ApiJson.Options
        );
        linkRes.EnsureSuccessStatusCode();

        var deleteRes = await client.DeleteAsync($"/referral-partners/{partnerId}");
        await Assert.That(deleteRes.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }
}

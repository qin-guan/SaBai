using System.Net.Http.Json;
using System.Text.Json;
using SaBai.API.Endpoints.Auth;
using SaBai.Tests.Support;

namespace SaBai.Tests.Auth;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class StaffUserTagTests(AppFixture fixture)
{
    [Test]
    public async Task StaffUser_SupportsMultipleFreeformTags_AndTagFiltering()
    {
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var userName = $"tagged_user_{suffix}";

        var createRes = await admin.PostAsJsonAsync(
            "/auth/users",
            new
            {
                userName,
                password = "Password123!",
                nickname = $"Tagged {suffix}",
                roles = new[] { "member", "pn" },
                tags = new[] { "Khmer-Speaking", "  Pediatric  ", "khmer-speaking" },
            },
            ApiJson.Options
        );
        await Assert.That(createRes.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var created = (await createRes.Content.ReadFromJsonAsync<UserResponse>(ApiJson.Options))!;
        await Assert.That(created.Tags.Count).IsEqualTo(2);
        await Assert.That(created.Tags).Contains("Khmer-Speaking");
        await Assert.That(created.Tags).Contains("Pediatric");
        await Assert.That(created.Roles).Contains("member");
        await Assert.That(created.Roles).Contains("pn");

        var filteredRes = await admin.GetAsync("/auth/users?tag=pediatric");
        await Assert.That(filteredRes.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var filteredBody = await filteredRes.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var matchedIds = filteredBody
            .GetProperty("users")
            .EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid())
            .ToList();
        await Assert.That(matchedIds).Contains(created.Id);

        var patchRes = await admin.PatchAsJsonAsync(
            $"/auth/users/{created.Id}",
            new { tags = new[] { "Translator", "Night-Shift" } },
            ApiJson.Options
        );
        await Assert.That(patchRes.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = (await patchRes.Content.ReadFromJsonAsync<UserResponse>(ApiJson.Options))!;
        await Assert.That(updated.Tags.Count).IsEqualTo(2);
        await Assert.That(updated.Tags).Contains("Night-Shift");
        await Assert.That(updated.Tags).Contains("Translator");

        var allTagsRes = await admin.GetAsync("/tags");
        await Assert.That(allTagsRes.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var allTagsBody = await allTagsRes.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var allTags = allTagsBody
            .GetProperty("tags")
            .EnumerateArray()
            .Select(x => x.GetString())
            .ToList();
        await Assert.That(allTags).Contains("Translator");
    }
}

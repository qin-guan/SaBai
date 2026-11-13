using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Auth;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class StaffUserTagTests(AppFixture fixture)
{
    [Test]
    public async Task CreateAndUpdateUser_SyncsMultipleTags()
    {
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var userName = $"tagged_{suffix}";

        // Create user with multiple tags
        var createdRes = await client.PostAsJsonAsync(
            "/auth/users",
            new
            {
                userName,
                password = "Password123!",
                nickname = $"Tagged {suffix}",
                roles = new[] { "member" },
                tags = new[] { "PN", "PL", "pn" },
            },
            ApiJson.Options
        );
        var createdBody = await createdRes.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var userId = createdBody.GetProperty("id").GetGuid();
        var createdTags = createdBody
            .GetProperty("tags")
            .EnumerateArray()
            .Select(x => x.GetString())
            .ToList();

        // Update user tags
        var updatedRes = await client.PatchAsJsonAsync(
            $"/auth/users/{userId}",
            new
            {
                id = userId,
                tags = new[] { "Dental", "PN" },
            },
            ApiJson.Options
        );
        var updatedBody = await updatedRes.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var updatedTags = updatedBody
            .GetProperty("tags")
            .EnumerateArray()
            .Select(x => x.GetString())
            .ToList();

        await Assert.That(createdRes.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(createdTags.Count).IsEqualTo(2);
        await Assert.That(createdTags).Contains("PN");
        await Assert.That(createdTags).Contains("PL");

        await Assert.That(updatedRes.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(updatedTags.Count).IsEqualTo(2);
        await Assert.That(updatedTags).Contains("Dental");
        await Assert.That(updatedTags).Contains("PN");
    }
}

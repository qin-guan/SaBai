using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Auth;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class PnRoleTests(AppFixture fixture)
{
    [Test]
    public async Task UserWithPnRole_CanAccessReferralsAndPartners()
    {
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var userName = $"pn_user_{suffix}";
        const string password = "Password123!";

        var createdRes = await admin.PostAsJsonAsync(
            "/auth/users",
            new
            {
                userName,
                password,
                nickname = $"PN {suffix}",
                roles = new[] { "pn" },
            },
            ApiJson.Options
        );
        var createdBody = await createdRes.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var roles = createdBody
            .GetProperty("roles")
            .EnumerateArray()
            .Select(x => x.GetString())
            .ToList();

        await Assert.That(createdRes.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(roles).Contains("pn");

        var pnClient = fixture.CreateApiClient();
        var loginRes = await pnClient.PostAsJsonAsync(
            "/auth/login",
            new { identifier = userName, password },
            ApiJson.Options
        );
        loginRes.EnsureSuccessStatusCode();

        var referralsRes = await pnClient.GetAsync("/referrals");
        var partnersRes = await pnClient.GetAsync("/referral-partners");
        var usersRes = await pnClient.GetAsync("/auth/users");

        await Assert.That(referralsRes.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(partnersRes.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(usersRes.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}

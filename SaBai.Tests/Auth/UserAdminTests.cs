using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Auth;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class UserAdminTests(AppFixture fixture)
{
    [Test]
    public async Task List_IncludesSeededAdmin()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.GetAsync("/auth/users");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).Contains(ClinicApi.AdminEmail);
    }

    [Test]
    public async Task Get_SignedInAdmin_ReturnsEmail()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var me = await client.WhoAmIAsync();

        // Act
        var response = await client.GetAsync($"/auth/users/{me.Id}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.GetProperty("email").GetString()).IsEqualTo(ClinicApi.AdminEmail);
    }

    [Test]
    public async Task Patch_Nickname_IsReturnedOnGet()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var nickname = $"Nick {suffix}";
        var created = await client.PostAsJsonAsync(
            "/auth/users",
            new
            {
                username = $"user{suffix}",
                email = $"user-{suffix}@sabai.test",
                password = ClinicApi.Password,
                nickname = "Before",
            },
            ApiJson.Options
        );
        created.EnsureSuccessStatusCode();
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var id = createdBody.GetProperty("id").GetGuid();

        // Act
        var patched = await client.PatchAsJsonAsync(
            $"/auth/users/{id}",
            new { nickname },
            ApiJson.Options
        );
        var fetched = await client.GetAsync($"/auth/users/{id}");
        var body = await fetched.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(patched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(fetched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.GetProperty("nickname").GetString()).IsEqualTo(nickname);
    }

    [Test]
    public async Task Login_EmptyPassword_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");

        // Act
        var response = await client.PostAsJsonAsync(
            "/auth/login",
            new { email = ClinicApi.AdminEmail, password = "" },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");

        // Act
        var response = await client.PostAsJsonAsync(
            "/auth/login",
            new
            {
                email = $"missing-{ClinicSetup.Suffix()}@sabai.test",
                password = ClinicApi.Password,
            },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Lock_AsMember_ReturnsForbidden()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.DoctorEmail);

        // Act
        var response = await client.PostAsync($"/auth/users/{Guid.NewGuid()}/lock", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Create_DuplicateAdminEmail_ReturnsConflict()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.PostAsJsonAsync(
            "/auth/users",
            new
            {
                username = $"dup{ClinicSetup.Suffix()}",
                email = ClinicApi.AdminEmail,
                password = ClinicApi.Password,
            },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Get_MissingUser_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.GetAsync("/auth/users/00000000-0000-0000-0000-000000000099");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task WhoAmI_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");

        // Act
        var response = await client.GetAsync("/auth/whoami");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}

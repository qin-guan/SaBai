using System.Net;
using System.Net.Http.Json;
using SaBai.API.Domain;
using SaBai.API.Endpoints.Auth;
using SaBai.Tests.Support;

namespace SaBai.Tests.Auth;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class AuthTests(AppFixture fixture)
{
    [Test]
    public async Task Healthcheck_ReturnsYay()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");

        // Act
        var response = await client.GetAsync("/healthcheck");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).Contains("yay!");
    }

    [Test]
    public async Task Login_WithSeededAdmin_ReturnsTokenAndWhoAmI()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");

        // Act
        await client.LoginAsync();
        var me = await client.WhoAmIAsync();

        // Assert
        await Assert.That(me.Email).IsEqualTo(ClinicApi.AdminEmail);
        await Assert.That(me.Role).IsEqualTo(StaffRole.Admin);
        await Assert.That(me.IsLocked).IsFalse();
    }

    [Test]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");

        // Act
        var response = await client.PostAsJsonAsync(
            "/auth/login",
            new { email = ClinicApi.AdminEmail, password = "not-the-password" },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
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

    [Test]
    public async Task CreateUser_AsMember_ReturnsForbidden()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.DoctorEmail);

        // Act
        var response = await client.PostAsJsonAsync(
            "/auth/users",
            new
            {
                username = $"member-{Guid.NewGuid():N}"[..20],
                email = $"nope-{Guid.NewGuid():N}@sabai.test",
                password = ClinicApi.Password,
            },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task CreateLockAndUnlock_PreventsThenAllowsLogin()
    {
        // Arrange
        var admin = fixture.CreateHttpClient("api");
        await admin.LoginAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"lock-{suffix}@sabai.test";
        var username = $"lock{suffix}";

        // Act
        var created = await admin.PostAsJsonAsync(
            "/auth/users",
            new
            {
                username,
                email,
                password = ClinicApi.Password,
                nickname = "Locked User",
                role = StaffRole.Member,
            },
            ApiJson.Options
        );
        var user = await created.Content.ReadFromJsonAsync<UserResponse>(ApiJson.Options);
        var locked = await admin.PostAsync($"/auth/users/{user!.Id}/lock", null);
        var lockedLogin = fixture.CreateHttpClient("api");
        var denied = await lockedLogin.PostAsJsonAsync(
            "/auth/login",
            new { email, password = ClinicApi.Password },
            ApiJson.Options
        );
        var unlocked = await admin.PostAsync($"/auth/users/{user.Id}/unlock", null);
        var allowed = fixture.CreateHttpClient("api");
        await allowed.LoginAsync(email);

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(created.Headers.Location).IsNotNull();
        await Assert.That(locked.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(denied.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(unlocked.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await allowed.WhoAmIAsync()).IsNotNull();
    }

    [Test]
    public async Task DeleteUser_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var me = await client.WhoAmIAsync();

        // Act
        var response = await client.DeleteAsync($"/auth/users/{me.Id}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}

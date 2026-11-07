using System.Net.Http.Json;
using SaBai.API.Auth;
using SaBai.API.Endpoints.Auth;
using SaBai.Tests.Support;
using AuthenticatorResponse = SaBai.API.Endpoints.Auth.Account.Authenticator.Post.Response;
using RecoveryCodesResponse = SaBai.API.Endpoints.Auth.Account.RecoveryCodes.Post.Response;
using TwoFactorStatus = SaBai.API.Endpoints.Auth.Account.TwoFactor.Get.Response;

namespace SaBai.Tests.Auth;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class AccountTests(AppFixture fixture)
{
    [Test]
    public async Task TwoFactor_SetupEnable_ThenSignInNeedsCode()
    {
        // Arrange
        var (userName, _) = await CreateUserAsync();
        var client = await fixture.CreateApiClient().LoginAsync(userName);

        // Act: set up the authenticator and confirm it with a code.
        var setup = await client.PostAsync("/auth/account/authenticator", null);
        client.UseCookiesFrom(setup);
        var key = (await setup.Content.ReadFromJsonAsync<AuthenticatorResponse>(ApiJson.Options))!;
        var enable = await client.PostAsJsonAsync(
            "/auth/account/two-factor/enable",
            new { code = Totp.Code(key.SharedKey) },
            ApiJson.Options
        );
        client.UseCookiesFrom(enable);
        var codes = await enable.Content.ReadFromJsonAsync<RecoveryCodesResponse>(ApiJson.Options);
        var status = await client.GetFromJsonAsync<TwoFactorStatus>(
            "/auth/account/two-factor",
            ApiJson.Options
        );
        var stillSignedIn = await client.GetAsync("/auth/whoami");

        // Act: sign in again from a fresh browser.
        var browser = fixture.CreateApiClient();
        var password = await browser.PostAsJsonAsync(
            "/auth/login",
            new { userName, password = ClinicApi.Password },
            ApiJson.Options
        );
        var passwordBody = await password.Content.ReadFromJsonAsync<SignInResponse>(
            ApiJson.Options
        );
        browser.UseCookiesFrom(password);
        var beforeCode = await browser.GetAsync("/auth/whoami");
        var wrongCode = await browser.PostAsJsonAsync(
            "/auth/login/two-factor",
            new { code = "000000" },
            ApiJson.Options
        );
        var rightCode = await browser.PostAsJsonAsync(
            "/auth/login/two-factor",
            new { code = Totp.Code(key.SharedKey) },
            ApiJson.Options
        );
        var rightBody = await rightCode.Content.ReadFromJsonAsync<SignInResponse>(
            ApiJson.Options
        );
        browser.UseCookiesFrom(rightCode);

        // Assert
        await Assert.That(setup.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(key.AuthenticatorUri).StartsWith("otpauth://totp/");
        await Assert.That(enable.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(codes!.RecoveryCodes.Count).IsEqualTo(10);
        await Assert.That(status!.IsEnabled).IsTrue();
        await Assert.That(status.RecoveryCodesLeft).IsEqualTo(10);
        await Assert.That(stillSignedIn.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(passwordBody!.RequiresTwoFactor).IsTrue();
        await Assert.That(passwordBody.User).IsNull();
        await Assert.That(beforeCode.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(wrongCode.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(rightCode.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(rightBody!.User!.UserName).IsEqualTo(userName);
        await Assert.That((await browser.WhoAmIAsync()).TwoFactorEnabled).IsTrue();
    }

    [Test]
    public async Task TwoFactor_RecoveryCode_WorksOnce()
    {
        // Arrange
        var (userName, _) = await CreateUserAsync();
        var client = await fixture.CreateApiClient().LoginAsync(userName);
        var codes = await EnableTwoFactorAsync(client);

        // Act
        var first = await SignInWithRecoveryCodeAsync(userName, codes.RecoveryCodes[0]);
        var reused = await SignInWithRecoveryCodeAsync(userName, codes.RecoveryCodes[0]);

        // Assert
        await Assert.That(first).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(reused).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task TwoFactor_Disable_NeedsPasswordThenSignInIsPasswordOnly()
    {
        // Arrange
        var (userName, _) = await CreateUserAsync();
        var client = await fixture.CreateApiClient().LoginAsync(userName);
        await EnableTwoFactorAsync(client);

        // Act
        var wrong = await client.PostAsJsonAsync(
            "/auth/account/two-factor/disable",
            new { password = "wrong-password" },
            ApiJson.Options
        );
        var disabled = await client.PostAsJsonAsync(
            "/auth/account/two-factor/disable",
            new { password = ClinicApi.Password },
            ApiJson.Options
        );
        var again = await fixture.CreateApiClient().LoginAsync(userName);

        // Assert
        await Assert.That(wrong.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(disabled.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That((await again.WhoAmIAsync()).TwoFactorEnabled).IsFalse();
    }

    [Test]
    public async Task AdminResetTwoFactor_LetsUserSignInWithPassword()
    {
        // Arrange
        var (userName, id) = await CreateUserAsync();
        var client = await fixture.CreateApiClient().LoginAsync(userName);
        await EnableTwoFactorAsync(client);
        var admin = await fixture.CreateApiClient().LoginAsync();

        // Act
        var reset = await admin.PostAsync($"/auth/users/{id}/two-factor/reset", null);
        var user = await reset.Content.ReadFromJsonAsync<UserResponse>(ApiJson.Options);
        var again = await fixture.CreateApiClient().LoginAsync(userName);

        // Assert
        await Assert.That(reset.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(user!.TwoFactorEnabled).IsFalse();
        await Assert.That(await again.WhoAmIAsync()).IsNotNull();
    }

    [Test]
    public async Task ChangePassword_OldPasswordStopsWorking()
    {
        // Arrange
        var (userName, _) = await CreateUserAsync();
        var client = await fixture.CreateApiClient().LoginAsync(userName);
        const string newPassword = "a-brand-new-password";

        // Act
        var wrongCurrent = await client.PostAsJsonAsync(
            "/auth/account/password",
            new { currentPassword = "nope-nope-nope", newPassword },
            ApiJson.Options
        );
        var changed = await client.PostAsJsonAsync(
            "/auth/account/password",
            new { currentPassword = ClinicApi.Password, newPassword },
            ApiJson.Options
        );
        client.UseCookiesFrom(changed);
        var stillSignedIn = await client.GetAsync("/auth/whoami");
        var oldPassword = await fixture
            .CreateApiClient()
            .PostAsJsonAsync(
                "/auth/login",
                new { userName, password = ClinicApi.Password },
                ApiJson.Options
            );
        var withNew = await fixture.CreateApiClient().LoginAsync(userName, newPassword);

        // Assert
        await Assert.That(wrongCurrent.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(changed.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(stillSignedIn.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(oldPassword.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(await withNew.WhoAmIAsync()).IsNotNull();
    }

    [Test]
    public async Task AdminSetPassword_EndsOpenSessionAndNewPasswordWorks()
    {
        // Arrange
        var (userName, id) = await CreateUserAsync();
        var session = await fixture.CreateApiClient().LoginAsync(userName);
        var admin = await fixture.CreateApiClient().LoginAsync();
        const string newPassword = "set-by-the-admin";

        // Act
        var tooShort = await admin.PostAsJsonAsync(
            $"/auth/users/{id}/password",
            new { password = "short" },
            ApiJson.Options
        );
        var reset = await admin.PostAsJsonAsync(
            $"/auth/users/{id}/password",
            new { password = newPassword },
            ApiJson.Options
        );
        var oldSession = await session.GetAsync("/auth/whoami");
        var withNew = await fixture.CreateApiClient().LoginAsync(userName, newPassword);

        // Assert
        await Assert.That(tooShort.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(reset.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(oldSession.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(await withNew.WhoAmIAsync()).IsNotNull();
    }

    [Test]
    public async Task Lock_EndsOpenSession()
    {
        // Arrange
        var (userName, id) = await CreateUserAsync();
        var session = await fixture.CreateApiClient().LoginAsync(userName);
        var admin = await fixture.CreateApiClient().LoginAsync();

        // Act
        var locked = await admin.PostAsync($"/auth/users/{id}/lock", null);
        var body = await locked.Content.ReadFromJsonAsync<UserResponse>(ApiJson.Options);
        var afterLock = await session.GetAsync("/auth/whoami");

        // Assert
        await Assert.That(body!.IsLocked).IsTrue();
        await Assert.That(afterLock.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RoleChange_ReachesOpenSessionWithoutSignOut()
    {
        // Arrange
        var (userName, id) = await CreateUserAsync();
        var session = await fixture.CreateApiClient().LoginAsync(userName);
        var admin = await fixture.CreateApiClient().LoginAsync();
        var before = await session.GetAsync("/auth/roles");

        // Act
        var promoted = await admin.PatchAsJsonAsync(
            $"/auth/users/{id}",
            new { roles = new[] { StaffRoles.Admin, StaffRoles.Member } },
            ApiJson.Options
        );
        var after = await session.GetAsync("/auth/roles");
        session.UseCookiesFrom(after);
        var demoted = await admin.PatchAsJsonAsync(
            $"/auth/users/{id}",
            new { roles = new[] { StaffRoles.Member } },
            ApiJson.Options
        );
        var afterDemotion = await session.GetAsync("/auth/roles");

        // Assert
        await Assert.That(before.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(promoted.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(after.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(demoted.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(afterDemotion.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Admin_CannotLockSelfOrDropOwnAdminRole()
    {
        // Arrange
        var admin = await fixture.CreateApiClient().LoginAsync();
        var me = await admin.WhoAmIAsync();

        // Act
        var lockSelf = await admin.PostAsync($"/auth/users/{me.Id}/lock", null);
        var demoteSelf = await admin.PatchAsJsonAsync(
            $"/auth/users/{me.Id}",
            new { roles = new[] { StaffRoles.Member } },
            ApiJson.Options
        );

        // Assert
        await Assert.That(lockSelf.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(demoteSelf.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That((await admin.WhoAmIAsync()).Roles).Contains(StaffRoles.Admin);
    }

    [Test]
    public async Task RepeatedFailedSignIns_LockTheAccount()
    {
        // Arrange
        var (userName, _) = await CreateUserAsync();
        var client = fixture.CreateApiClient();

        // Act
        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync(
                "/auth/login",
                new { userName, password = "wrong-password" },
                ApiJson.Options
            );
        }

        var withRightPassword = await client.PostAsJsonAsync(
            "/auth/login",
            new { userName, password = ClinicApi.Password },
            ApiJson.Options
        );
        var admin = await fixture.CreateApiClient().LoginAsync();
        var listed = await admin.GetFromJsonAsync<SaBai.API.Endpoints.Auth.Users.List.Get.Response>(
            "/auth/users?isLocked=true",
            ApiJson.Options
        );

        // Assert
        await Assert.That(withRightPassword.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(listed!.Users.Select(x => x.UserName)).Contains(userName);
    }

    [Test]
    public async Task Logout_ClearsSession()
    {
        // Arrange
        var client = await fixture.CreateApiClient().LoginAsync(ClinicApi.DoctorEmail);

        // Act
        var logout = await client.PostAsync("/auth/logout", null);
        client.UseCookiesFrom(logout);
        var after = await client.GetAsync("/auth/whoami");

        // Assert
        await Assert.That(logout.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(after.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Roles_ListsAdminAndMember()
    {
        // Arrange
        var admin = await fixture.CreateApiClient().LoginAsync();

        // Act
        var roles = await admin.GetFromJsonAsync<SaBai.API.Endpoints.Auth.Roles.List.Get.Response>(
            "/auth/roles",
            ApiJson.Options
        );

        // Assert
        await Assert.That(roles!.Roles).Contains(StaffRoles.Admin);
        await Assert.That(roles.Roles).Contains(StaffRoles.Member);
    }

    [Test]
    public async Task Create_WithoutEmail_CanSignInByUserName()
    {
        // Arrange
        var admin = await fixture.CreateApiClient().LoginAsync();
        var userName = $"noemail{ClinicSetup.Suffix()}";

        // Act
        var created = await admin.PostAsJsonAsync(
            "/auth/users",
            new { userName, password = ClinicApi.Password },
            ApiJson.Options
        );
        var body = await created.Content.ReadFromJsonAsync<UserResponse>(ApiJson.Options);
        var me = await (await fixture.CreateApiClient().LoginAsync(userName)).WhoAmIAsync();

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(body!.Email).IsNull();
        await Assert.That(body.Roles).IsEquivalentTo(new[] { StaffRoles.Member });
        await Assert.That(me.Nickname).IsEqualTo(userName);
    }

    private async Task<(string UserName, Guid Id)> CreateUserAsync()
    {
        var admin = await fixture.CreateApiClient().LoginAsync();
        var userName = $"staff{ClinicSetup.Suffix()}";
        var created = await admin.PostAsJsonAsync(
            "/auth/users",
            new
            {
                userName,
                email = $"{userName}@sabai.test",
                password = ClinicApi.Password,
                roles = new[] { StaffRoles.Member },
            },
            ApiJson.Options
        );
        created.EnsureSuccessStatusCode();
        var user = await created.Content.ReadFromJsonAsync<UserResponse>(ApiJson.Options);
        return (userName, user!.Id);
    }

    private static async Task<RecoveryCodesResponse> EnableTwoFactorAsync(HttpClient client)
    {
        var setup = await client.PostAsync("/auth/account/authenticator", null);
        setup.EnsureSuccessStatusCode();
        client.UseCookiesFrom(setup);
        var key = (await setup.Content.ReadFromJsonAsync<AuthenticatorResponse>(ApiJson.Options))!;
        var enable = await client.PostAsJsonAsync(
            "/auth/account/two-factor/enable",
            new { code = Totp.Code(key.SharedKey) },
            ApiJson.Options
        );
        enable.EnsureSuccessStatusCode();
        client.UseCookiesFrom(enable);
        return (await enable.Content.ReadFromJsonAsync<RecoveryCodesResponse>(ApiJson.Options))!;
    }

    private async Task<HttpStatusCode> SignInWithRecoveryCodeAsync(string userName, string code)
    {
        var browser = fixture.CreateApiClient();
        var password = await browser.PostAsJsonAsync(
            "/auth/login",
            new { userName, password = ClinicApi.Password },
            ApiJson.Options
        );
        browser.UseCookiesFrom(password);
        var recovery = await browser.PostAsJsonAsync(
            "/auth/login/recovery-code",
            new { recoveryCode = code },
            ApiJson.Options
        );
        return recovery.StatusCode;
    }
}

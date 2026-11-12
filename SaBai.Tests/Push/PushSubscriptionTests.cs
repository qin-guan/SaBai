using System.Net.Http.Json;
using SaBai.Tests.Support;
using PushConfig = SaBai.API.Endpoints.Push.Config.Get.Response;
using PushTestResult = SaBai.API.Endpoints.Push.Test.Post.Response;

namespace SaBai.Tests.Push;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class PushSubscriptionTests(AppFixture fixture)
{
    // A real-looking device key pair: an uncompressed P-256 point and a 16-byte secret.
    private const string P256dh =
        "BJjWYdY98l44WgyWfb56xewn_tGYTsk4groBBbEI5_3v8oksOOJjFo4QrYp5FI6ZPvL_5KavTUeto2e8dOmKe9w";
    private const string Auth = "nBRULuBJDePGN_eQ2uLc9g";

    private static string NewEndpoint() =>
        $"https://fcm.googleapis.com/fcm/send/{Guid.NewGuid():N}";

    private static object Subscription(string endpoint, string p256dh = P256dh, string auth = Auth) =>
        new { endpoint, keys = new { p256dh, auth } };

    private static async Task<PushConfig> ConfigAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<PushConfig>("/push/config", ApiJson.Options))!;

    [Test]
    public async Task Config_ReturnsTheServerPublicKey()
    {
        // Arrange
        var client = await fixture.CreateApiClient().LoginAsync(ClinicApi.DoctorEmail);

        // Act
        var config = await ConfigAsync(client);

        // Assert
        await Assert.That(config.IsEnabled).IsTrue();
        await Assert.That(config.PublicKey).IsEqualTo(AppFixture.PushPublicKey);
    }

    [Test]
    public async Task Endpoints_NeedASignedInUser()
    {
        // Arrange
        var client = fixture.CreateApiClient();

        // Act
        var config = await client.GetAsync("/push/config");
        var subscribe = await client.PutAsJsonAsync(
            "/push/subscriptions",
            Subscription(NewEndpoint()),
            ApiJson.Options
        );
        var test = await client.PostAsync("/push/test", null);

        // Assert
        await Assert.That(config.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(subscribe.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(test.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Subscribe_IsRepeatable_AndUnsubscribeRemovesTheDevice()
    {
        // Arrange
        var client = await fixture.CreateApiClient().LoginAsync(ClinicApi.PharmacistEmail);
        var endpoint = NewEndpoint();
        var before = (await ConfigAsync(client)).DeviceCount;

        // Act
        var first = await client.PutAsJsonAsync(
            "/push/subscriptions",
            Subscription(endpoint),
            ApiJson.Options
        );
        var again = await client.PutAsJsonAsync(
            "/push/subscriptions",
            Subscription(endpoint),
            ApiJson.Options
        );
        var subscribed = (await ConfigAsync(client)).DeviceCount;
        var remove = await client.PostAsJsonAsync(
            "/push/subscriptions/unsubscribe",
            new { endpoint },
            ApiJson.Options
        );
        var removeAgain = await client.PostAsJsonAsync(
            "/push/subscriptions/unsubscribe",
            new { endpoint },
            ApiJson.Options
        );
        var after = (await ConfigAsync(client)).DeviceCount;

        // Assert
        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(again.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(subscribed).IsEqualTo(before + 1);
        await Assert.That(remove.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(removeAgain.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(after).IsEqualTo(before);
    }

    [Test]
    public async Task Subscribe_SameDeviceByAnotherUser_MovesItToThatUser()
    {
        // Arrange: two staff share one device.
        var (firstUser, secondUser) = (await NewUserAsync(), await NewUserAsync());
        var first = await fixture.CreateApiClient().LoginAsync(firstUser);
        var second = await fixture.CreateApiClient().LoginAsync(secondUser);
        var endpoint = NewEndpoint();
        await first.PutAsJsonAsync("/push/subscriptions", Subscription(endpoint), ApiJson.Options);

        // Act
        var takeOver = await second.PutAsJsonAsync(
            "/push/subscriptions",
            Subscription(endpoint),
            ApiJson.Options
        );
        var firstCount = (await ConfigAsync(first)).DeviceCount;
        var secondCount = (await ConfigAsync(second)).DeviceCount;
        // The first user can no longer remove a device that is not theirs.
        await first.PostAsJsonAsync(
            "/push/subscriptions/unsubscribe",
            new { endpoint },
            ApiJson.Options
        );
        var secondCountAfter = (await ConfigAsync(second)).DeviceCount;

        // Assert
        await Assert.That(takeOver.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(firstCount).IsEqualTo(0);
        await Assert.That(secondCount).IsEqualTo(1);
        await Assert.That(secondCountAfter).IsEqualTo(1);
    }

    [Test]
    [Arguments("http://fcm.googleapis.com/fcm/send/abc")]
    [Arguments("https://example.com/push/abc")]
    [Arguments("https://fcm.googleapis.com.evil.example/abc")]
    [Arguments("https://localhost/abc")]
    [Arguments("not a url")]
    public async Task Subscribe_EndpointOutsideKnownPushServices_IsRefused(string endpoint)
    {
        // Arrange
        var client = await fixture.CreateApiClient().LoginAsync();

        // Act
        var response = await client.PutAsJsonAsync(
            "/push/subscriptions",
            Subscription(endpoint),
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    [Arguments("", Auth)]
    [Arguments("AAAA", Auth)]
    [Arguments(P256dh, "AAAA")]
    [Arguments(P256dh, "not base64 !!")]
    public async Task Subscribe_MalformedKeys_AreRefused(string p256dh, string auth)
    {
        // Arrange
        var client = await fixture.CreateApiClient().LoginAsync();

        // Act
        var response = await client.PutAsJsonAsync(
            "/push/subscriptions",
            Subscription(NewEndpoint(), p256dh, auth),
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Test_WithNoDevices_SendsNothing()
    {
        // Arrange
        var client = await fixture.CreateApiClient().LoginAsync(await NewUserAsync());

        // Act
        var response = await client.PostAsync("/push/test", null);
        var result = await response.Content.ReadFromJsonAsync<PushTestResult>(ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(result).IsEqualTo(new PushTestResult(0, 0, 0));
    }

    private async Task<string> NewUserAsync()
    {
        var admin = await fixture.CreateApiClient().LoginAsync();
        var userName = $"push{ClinicSetup.Suffix()}";
        var created = await admin.PostAsJsonAsync(
            "/auth/users",
            new
            {
                userName,
                nickname = "Push Tester",
                password = ClinicApi.Password,
                roles = new[] { "member" },
            },
            ApiJson.Options
        );
        created.EnsureSuccessStatusCode();
        return userName;
    }
}

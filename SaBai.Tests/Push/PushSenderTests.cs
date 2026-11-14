using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SaBai.API.Domain;
using SaBai.API.Push;
using SaBai.Tests.Support;

namespace SaBai.Tests.Push;

public class PushSenderTests
{
    private static readonly PushSettings Settings = new()
    {
        Subject = "mailto:push@sabai.test",
        PublicKey = AppFixture.PushPublicKey,
        PrivateKey = AppFixture.PushPrivateKey,
    };

    private static readonly StaffPushSubscription Device = new()
    {
        Id = Guid.CreateVersion7(),
        Endpoint = "https://fcm.googleapis.com/fcm/send/device-1",
        P256dhKey =
            "BJjWYdY98l44WgyWfb56xewn_tGYTsk4groBBbEI5_3v8oksOOJjFo4QrYp5FI6ZPvL_5KavTUeto2e8dOmKe9w",
        AuthSecret = "nBRULuBJDePGN_eQ2uLc9g",
    };

    private sealed class FakePushService(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public byte[] Body { get; private set; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Request = request;
            Body = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            return new HttpResponseMessage(status);
        }
    }

    private static PushSender Sender(HttpMessageHandler service, PushSettings? settings = null) =>
        new(
            new HttpClient(service),
            Options.Create(settings ?? Settings),
            NullLogger<PushSender>.Instance
        );

    [Test]
    public async Task Send_PostsASignedMessageOnlyTheDeviceCanRead()
    {
        // Arrange: a device with its own key pair, as a browser creates when it subscribes.
        using var deviceKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var authSecret = RandomNumberGenerator.GetBytes(16);
        var device = new StaffPushSubscription
        {
            Endpoint = Device.Endpoint,
            P256dhKey = WebEncoders.Base64UrlEncode(PublicPoint(deviceKey)),
            AuthSecret = WebEncoders.Base64UrlEncode(authSecret),
        };
        var service = new FakePushService(HttpStatusCode.Created);

        // Act
        var outcome = await Sender(service)
            .SendAsync(
                device,
                new PushNotification("Pharmacy", "Order ready", Url: "/patient/12", Tag: "order-7"),
                CancellationToken.None
            );

        // Assert
        var request = service.Request!;
        await Assert.That(outcome).IsEqualTo(PushOutcome.Sent);
        await Assert.That(request.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(request.RequestUri!.AbsoluteUri).IsEqualTo(Device.Endpoint);
        await Assert.That(request.Headers.Authorization!.Scheme).IsEqualTo("vapid");
        await Assert.That(request.Headers.Authorization.Parameter!).Contains(Settings.PublicKey);
        await Assert.That(request.Headers.GetValues("TTL").Single()).IsEqualTo("43200");
        await Assert.That(request.Content!.Headers.ContentEncoding.Single()).IsEqualTo("aes128gcm");
        // The body is ciphertext on the wire, and decrypts on the device to the
        // JSON the service worker reads (camelCase, unset fields left out).
        await Assert.That(Encoding.UTF8.GetString(service.Body)).DoesNotContain("Pharmacy");
        await Assert
            .That(Decrypt(service.Body, deviceKey, authSecret))
            .IsEqualTo(
                """{"title":"Pharmacy","body":"Order ready","url":"/patient/12","tag":"order-7"}"""
            );
    }

    private static byte[] PublicPoint(ECDiffieHellman key)
    {
        var q = key.ExportParameters(false).Q;
        return [0x04, .. q.X!, .. q.Y!];
    }

    /// <summary>Decrypts a Web Push message body the way a browser does (RFC 8291, aes128gcm).</summary>
    private static string Decrypt(byte[] body, ECDiffieHellman deviceKey, byte[] authSecret)
    {
        var salt = body[..16];
        var keyIdLength = body[20];
        var senderPoint = body[21..(21 + keyIdLength)];
        var cipher = body[(21 + keyIdLength)..];

        using var sender = ECDiffieHellman.Create(
            new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = senderPoint[1..33], Y = senderPoint[33..65] },
            }
        );
        var shared = deviceKey.DeriveRawSecretAgreement(sender.PublicKey);
        byte[] keyInfo =
        [
            .. Encoding.ASCII.GetBytes("WebPush: info\0"),
            .. PublicPoint(deviceKey),
            .. senderPoint,
        ];
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, authSecret, keyInfo);
        var prk = HKDF.Extract(HashAlgorithmName.SHA256, ikm, salt);
        var key = HKDF.Expand(
            HashAlgorithmName.SHA256,
            prk,
            16,
            Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0")
        );
        var nonce = HKDF.Expand(
            HashAlgorithmName.SHA256,
            prk,
            12,
            Encoding.ASCII.GetBytes("Content-Encoding: nonce\0")
        );

        var plain = new byte[cipher.Length - 16];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, cipher[..^16], cipher[^16..], plain);
        // The record ends with a 0x02 delimiter and optional zero padding.
        return Encoding.UTF8.GetString(plain, 0, Array.LastIndexOf(plain, (byte)2));
    }

    [Test]
    [Arguments(HttpStatusCode.Gone, PushOutcome.Gone)]
    [Arguments(HttpStatusCode.NotFound, PushOutcome.Gone)]
    [Arguments(HttpStatusCode.InternalServerError, PushOutcome.Failed)]
    [Arguments(HttpStatusCode.Forbidden, PushOutcome.Failed)]
    public async Task Send_ReportsWhatThePushServiceAnswered(HttpStatusCode status, PushOutcome expected)
    {
        // Act
        var outcome = await Sender(new FakePushService(status))
            .SendAsync(Device, new PushNotification("Title"), CancellationToken.None);

        // Assert
        await Assert.That(outcome).IsEqualTo(expected);
    }

    [Test]
    public async Task Settings_AreOffUntilKeysAndSubjectAreValid()
    {
        await Assert.That(Settings.IsEnabled).IsTrue();
        await Assert.That(new PushSettings().IsEnabled).IsFalse();
        await Assert
            .That(new PushSettings { Subject = Settings.Subject, PublicKey = "abc", PrivateKey = Settings.PrivateKey }.IsEnabled)
            .IsFalse();
        await Assert
            .That(new PushSettings { Subject = Settings.Subject, PublicKey = Settings.PublicKey, PrivateKey = "abc" }.IsEnabled)
            .IsFalse();
        await Assert
            .That(new PushSettings { Subject = "someone", PublicKey = Settings.PublicKey, PrivateKey = Settings.PrivateKey }.IsEnabled)
            .IsFalse();
    }

    [Test]
    [Arguments("https://fcm.googleapis.com/fcm/send/abc", true)]
    [Arguments("https://jmt17.google.com/fcm/send/abc", true)]
    [Arguments("https://updates.push.services.mozilla.com/wpush/v2/abc", true)]
    [Arguments("https://web.push.apple.com/abc", true)]
    [Arguments("https://wns2-sg2p.notify.windows.com/w/?token=abc", true)]
    [Arguments("http://fcm.googleapis.com/fcm/send/abc", false)]
    [Arguments("https://evilfcm.googleapis.com.example/abc", false)]
    [Arguments("https://user@fcm.googleapis.com/abc", false)]
    [Arguments("https://127.0.0.1/abc", false)]
    [Arguments("/relative", false)]
    public async Task Settings_AllowOnlyKnownPushServices(string endpoint, bool allowed)
    {
        await Assert.That(Settings.IsAllowedEndpoint(endpoint)).IsEqualTo(allowed);
    }
}

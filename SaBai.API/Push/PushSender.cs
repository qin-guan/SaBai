using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Options;
using SaBai.API.Domain;

namespace SaBai.API.Push;

/// <summary>
/// A notification as the web app's service worker reads it
/// (<c>SaBai.Web.Nuxt/app/utils/pushPayload.ts</c>).
/// </summary>
/// <param name="Url">Path in the web app to open when the notification is tapped.</param>
/// <param name="Tag">Notifications with the same tag replace each other.</param>
public sealed record PushNotification(
    string Title,
    string? Body = null,
    string? Url = null,
    string? Tag = null,
    bool? RequireInteraction = null
);

public enum PushOutcome
{
    /// <summary>The push service accepted the message.</summary>
    Sent,

    /// <summary>The subscription no longer exists (the app was removed or permission withdrawn).</summary>
    Gone,

    /// <summary>The push service could not be reached or refused the message.</summary>
    Failed,
}

/// <summary>
/// Encrypts a notification for one device and posts it to that device's push
/// service (Web Push with VAPID). It does not touch the database.
/// </summary>
public sealed class PushSender(
    HttpClient http,
    IOptions<PushSettings> settings,
    ILogger<PushSender> logger
)
{
    private static readonly JsonSerializerOptions PayloadJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>How long a push service keeps a message for a device that is offline.</summary>
    private static readonly TimeSpan TimeToLive = TimeSpan.FromHours(12);

    private readonly PushServiceClient _client = new(http);

    public async Task<PushOutcome> SendAsync(
        StaffPushSubscription subscription,
        PushNotification notification,
        CancellationToken ct
    )
    {
        var push = settings.Value;
        if (!push.IsEnabled)
        {
            throw new InvalidOperationException(push.Problem);
        }

        var target = new PushSubscription { Endpoint = subscription.Endpoint };
        target.SetKey(PushEncryptionKeyName.P256DH, subscription.P256dhKey);
        target.SetKey(PushEncryptionKeyName.Auth, subscription.AuthSecret);
        var message = new PushMessage(JsonSerializer.Serialize(notification, PayloadJson))
        {
            TimeToLive = (int)TimeToLive.TotalSeconds,
            Urgency = PushMessageUrgency.High,
        };

        try
        {
            using var vapid = new VapidAuthentication(push.PublicKey, push.PrivateKey)
            {
                Subject = push.Subject,
            };
            await _client.RequestPushMessageDeliveryAsync(target, message, vapid, ct);
            return PushOutcome.Sent;
        }
        catch (PushServiceClientException ex)
            when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return PushOutcome.Gone;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // The endpoint URL is the device's address; log only which service it is.
            logger.LogWarning(
                ex,
                "Push message to {Host} for subscription {Id} failed",
                new Uri(subscription.Endpoint).Host,
                subscription.Id
            );
            return PushOutcome.Failed;
        }
    }
}

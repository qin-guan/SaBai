using FastEndpoints;
using Microsoft.Extensions.Options;
using SaBai.API.Auth;
using SaBai.API.Domain;
using SaBai.API.Push;

namespace SaBai.API.Endpoints.Push.Test.Post;

public sealed class Endpoint(PushNotifier notifier, IOptions<PushSettings> settings)
    : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Post("/push/test");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Send a test notification to your own devices");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!settings.Value.IsEnabled)
        {
            throw new ConflictException("Notifications are not set up on this server.");
        }

        var result = await notifier.NotifyUserAsync(
            User.Id(),
            new PushNotification(
                "Sa'Bai",
                "Notifications are working on this device.",
                Url: "/account",
                Tag: "push-test"
            ),
            ct
        );
        await Send.OkAsync(new Response(result.Sent, result.Removed, result.Failed), ct);
    }
}

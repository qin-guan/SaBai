using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Push;

namespace SaBai.API.Endpoints.Push.Config.Get;

public sealed class Endpoint(AppDbContext db, IOptions<PushSettings> settings)
    : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Get("/push/config");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s =>
        {
            s.Summary = "Get the push notification settings a browser needs to subscribe";
            s.Description = "Notifications are off until the server has a VAPID key pair.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var push = settings.Value;
        var userId = User.Id();
        var devices = await db.PushSubscriptions.CountAsync(x => x.StaffUserId == userId, ct);
        await Send.OkAsync(
            new Response(push.IsEnabled, push.IsEnabled ? push.PublicKey : null, devices),
            ct
        );
    }
}

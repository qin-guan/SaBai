using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;
using SaBai.API.Push;

namespace SaBai.API.Endpoints.Push.Subscribe.Put;

public sealed class Endpoint(AppDbContext db, IOptions<PushSettings> settings) : Endpoint<Request>
{
    public override void Configure()
    {
        Put("/push/subscriptions");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s =>
        {
            s.Summary = "Receive notifications on this device";
            s.Description =
                "Saves the browser's push subscription for the signed-in user. Safe to repeat; "
                + "a device that another user subscribed earlier moves to the signed-in user.";
        });
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var push = settings.Value;
        if (!push.IsEnabled)
        {
            throw new ConflictException("Notifications are not set up on this server.");
        }

        if (!push.IsAllowedEndpoint(req.Endpoint))
        {
            throw new BadRequestException("This browser's push service is not supported.");
        }

        var subscription = await db.PushSubscriptions.FirstOrDefaultAsync(
            x => x.Endpoint == req.Endpoint,
            ct
        );
        if (subscription is null)
        {
            subscription = new StaffPushSubscription
            {
                Endpoint = req.Endpoint,
                P256dhKey = req.Keys.P256dh,
                AuthSecret = req.Keys.Auth,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.PushSubscriptions.Add(subscription);
        }

        subscription.StaffUserId = User.Id();
        subscription.P256dhKey = req.Keys.P256dh;
        subscription.AuthSecret = req.Keys.Auth;
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

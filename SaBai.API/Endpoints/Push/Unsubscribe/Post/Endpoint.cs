using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Push.Unsubscribe.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Post("/push/subscriptions/unsubscribe");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s =>
        {
            s.Summary = "Stop notifications on this device";
            s.Description =
                "Removes the signed-in user's subscription for this device. "
                + "Succeeds when there is none.";
        });
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var userId = User.Id();
        var subscription = await db.PushSubscriptions.FirstOrDefaultAsync(
            x => x.Endpoint == req.Endpoint && x.StaffUserId == userId,
            ct
        );
        if (subscription is not null)
        {
            db.PushSubscriptions.Remove(subscription);
            await db.SaveChangesAsync(ct);
        }

        await Send.NoContentAsync(ct);
    }
}

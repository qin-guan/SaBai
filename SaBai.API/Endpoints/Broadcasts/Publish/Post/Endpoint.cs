using FastEndpoints;
using Microsoft.Extensions.Options;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Push;

namespace SaBai.API.Endpoints.Broadcasts.Publish.Post;

public sealed class Endpoint(
    AppDbContext db,
    PushNotifier notifier,
    IOptions<PushSettings> settings
) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/broadcasts/{Id}/publish");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Publish a broadcast");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var broadcast = await db.Broadcasts.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Broadcast was not found.",
            ct
        );
        if (broadcast.PublishedAt is null)
        {
            broadcast.PublishedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        if (settings.Value.IsEnabled)
        {
            await notifier.NotifyEveryoneAsync(
                new PushNotification(
                    broadcast.Title,
                    broadcast.Body,
                    Url: "/settings/notifications",
                    Tag: $"broadcast-{broadcast.Id}"
                ),
                ct
            );
        }

        await Send.OkAsync(
            new Response(
                broadcast.Id,
                broadcast.Title,
                broadcast.Body,
                broadcast.Severity,
                broadcast.CreatedBy,
                broadcast.CreatedAt,
                broadcast.PublishedAt,
                broadcast.Attempts
            ),
            ct
        );
    }
}

using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Broadcasts.Publish.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
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

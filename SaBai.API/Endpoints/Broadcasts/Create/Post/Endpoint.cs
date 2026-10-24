using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Broadcasts.Create.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/broadcasts");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Create a broadcast");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var broadcast = new Broadcast
        {
            Title = req.Title.Trim(),
            Body = req.Body.Trim(),
            Severity = req.Severity,
            CreatedBy = User.Email(),
            CreatedAt = DateTimeOffset.UtcNow,
            PublishedAt = null,
            Attempts = 0,
        };
        db.Broadcasts.Add(broadcast);
        await db.SaveChangesAsync(ct);

        var response = new Response(
            broadcast.Id,
            broadcast.Title,
            broadcast.Body,
            broadcast.Severity,
            broadcast.CreatedBy,
            broadcast.CreatedAt,
            broadcast.PublishedAt,
            broadcast.Attempts
        );
        await Send.CreatedAtAsync<List.Get.Endpoint>(new { }, response, cancellation: ct);
    }
}

using System.Security.Claims;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Sync.Rejections.Dismiss.Post;

public sealed record Request(Guid Id);

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Post("/sync/rejections/{Id}/dismiss");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Dismiss a sync rejection after staff review");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var rejection = await db.SyncRejections.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (rejection is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)
            ? uid
            : (Guid?)null;

        rejection.ResolvedAt = DateTimeOffset.UtcNow;
        rejection.ResolvedByUserId = userId;
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

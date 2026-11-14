using System.Security.Claims;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Sync.Conflicts.Resolve.Post;

public sealed record Request(Guid Id);

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Post("/sync/conflicts/{Id}/resolve");
        Roles(StaffRoles.Admin, StaffRoles.Member, StaffRoles.Pn);
        Summary(s => s.Summary = "Mark a sync conflict as reviewed/resolved");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var conflict = await db.SyncConflicts.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (conflict is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)
            ? uid
            : (Guid?)null;

        conflict.ResolvedAt = DateTimeOffset.UtcNow;
        conflict.ResolvedByUserId = userId;
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

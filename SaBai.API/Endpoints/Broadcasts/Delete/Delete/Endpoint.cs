using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Broadcasts.Delete.Delete;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/broadcasts/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Delete a broadcast");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var broadcast = await db.Broadcasts.FirstOrNotFoundAsync(x => x.Id == req.Id, "Broadcast was not found.", ct);
        db.Broadcasts.Remove(broadcast);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

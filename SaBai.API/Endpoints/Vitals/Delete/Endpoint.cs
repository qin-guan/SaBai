using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Vitals.Delete;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/vitals/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Delete vitals");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var entity = await db.Vitals.FirstOrNotFoundAsync(x => x.Id == req.Id, "Vitals were not found.", ct);
        db.Vitals.Remove(entity);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

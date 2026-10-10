using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.ChildVitals.Delete;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/child-vitals/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Delete child vitals");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var entity = await db.ChildVitals.FirstOrNotFoundAsync(x => x.Id == req.Id, "Child vitals were not found.", ct);
        db.ChildVitals.Remove(entity);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

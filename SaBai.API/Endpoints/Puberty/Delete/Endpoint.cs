using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Puberty.Delete;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/puberty/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Delete a puberty assessment");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var entity = await db.PubertyAssessments.FirstOrNotFoundAsync(x => x.Id == req.Id, "Puberty assessment was not found.", ct);
        db.PubertyAssessments.Remove(entity);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

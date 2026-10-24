using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Eyesight.Delete;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/eyesight/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Delete eyesight");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var entity = await db.Eyesights.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Eyesight was not found.",
            ct
        );
        db.Eyesights.Remove(entity);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Visits.Delete.Delete;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/visits/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Delete a visit");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var visit = await db.Visits.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Visit was not found.",
            ct
        );
        if (await db.Consults.AnyAsync(x => x.VisitId == visit.Id, ct))
        {
            throw new ConflictException("A consult still references this visit.");
        }

        db.Visits.Remove(visit);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

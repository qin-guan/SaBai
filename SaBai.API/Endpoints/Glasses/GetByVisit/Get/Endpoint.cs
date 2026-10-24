using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Glasses.GetByVisit.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/glasses");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Get prescribed glasses for a visit");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var eyesight = await db.Eyesights
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .FirstOrNotFoundAsync(x => x.VisitId == req.VisitId, "Glasses were not found.", ct);
        await Send.OkAsync(Response.From(eyesight), ct);
    }
}

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Eyesight.GetByVisit.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/visits/{VisitId}/eyesight");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Get eyesight for a visit");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var entity = await db.Eyesights
            .AsNoTracking()
            .FirstOrNotFoundAsync(x => x.VisitId == req.VisitId, "Eyesight was not found.", ct);
        await Send.OkAsync(Response.From(entity), ct);
    }
}

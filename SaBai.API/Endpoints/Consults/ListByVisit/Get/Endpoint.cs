using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Consults.ListByVisit.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, List<Response>>
{
    public override void Configure()
    {
        Get("/visits/{VisitId}/consults");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "List consults for a visit, newest first");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        _ = await db
            .Visits.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.VisitId, "Visit was not found.", ct);
        var consults = await db
            .Consults.AsNoTracking()
            .Include(x => x.Doctor)
            .Include(x => x.Diagnoses)
            .Where(x => x.VisitId == req.VisitId)
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);

        await Send.OkAsync(
            [
                .. consults.Select(
                    global::SaBai.API.Endpoints.Consults.ListByVisit.Get.Response.From
                ),
            ],
            ct
        );
    }
}

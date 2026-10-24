using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Puberty.GetByVitals.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/vitals/{VitalsId}/puberty");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Get the puberty assessment for vitals");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var entity = await db
            .PubertyAssessments.AsNoTracking()
            .FirstOrNotFoundAsync(
                x => x.VitalsId == req.VitalsId,
                "Puberty assessment was not found.",
                ct
            );
        await Send.OkAsync(Response.From(entity), ct);
    }
}

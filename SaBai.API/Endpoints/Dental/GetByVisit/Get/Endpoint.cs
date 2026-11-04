using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Dental.GetByVisit.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, DentalConsultResponse>
{
    public override void Configure()
    {
        Get("/visits/{VisitId}/dental");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Get the dental consultation for a visit");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var entity = await db
            .DentalConsults.AsNoTracking()
            .FirstOrNotFoundAsync(
                x => x.VisitId == req.VisitId,
                "Dental consultation was not found.",
                ct
            );
        await Send.OkAsync(DentalConsultResponse.From(entity), ct);
    }
}

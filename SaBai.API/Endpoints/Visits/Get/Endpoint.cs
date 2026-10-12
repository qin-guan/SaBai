using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Visits.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/visits/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Get a visit");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await db
            .Visits.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.Id, "Visit was not found.", ct);
        var visit = await db
            .Visits.AsNoTracking()
            .Where(x => x.Id == req.Id)
            .Select(x => new Response(
                x.Id,
                x.Date,
                x.PatientId,
                x.Patient.Name,
                x.VillageCodeId,
                x.VillageCode.Code,
                x.VillageCode.Name,
                x.VillageCode.ColorHex,
                x.Status
            ))
            .FirstAsync(ct);

        await Send.OkAsync(visit, ct);
    }
}

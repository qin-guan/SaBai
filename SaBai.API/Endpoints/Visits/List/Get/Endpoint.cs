using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Visits.List.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/visits");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "List visits");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var query = db.Visits.AsNoTracking().AsQueryable();
        if (req.PatientId is int patientId)
        {
            query = query.Where(x => x.PatientId == patientId);
        }

        var visits = await query
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.Id)
            .Select(x => new Item(
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
            .ToListAsync(ct);

        await Send.OkAsync(new Response(visits), ct);
    }
}

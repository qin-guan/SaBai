using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Patients.Visits.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/patients/{PatientId}/visits");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "List visits for a patient");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await db
            .Patients.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.PatientId, "Patient was not found.", ct);

        var visits = await db
            .Visits.AsNoTracking()
            .Where(x => x.PatientId == req.PatientId)
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Date,
                x.PatientId,
                PatientName = x.Patient.Name,
                x.VillageCodeId,
                VillageCode = x.VillageCode.Code,
                VillageName = x.VillageCode.Name,
                VillageColorHex = x.VillageCode.ColorHex,
                x.Status,
            })
            .ToListAsync(ct);
        var journeys = await VisitJourney.ForVisitsAsync(
            db.Visits,
            [.. visits.Select(x => x.Id)],
            ct
        );

        var items = visits
            .Select(x => new Item(
                x.Id,
                x.Date,
                x.PatientId,
                x.PatientName,
                x.VillageCodeId,
                x.VillageCode,
                x.VillageName,
                x.VillageColorHex,
                x.Status,
                journeys.GetValueOrDefault(x.Id, VisitJourney.Empty)
            ))
            .ToList();

        await Send.OkAsync(new Response(items), ct);
    }
}

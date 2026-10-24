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
        await db.Patients.AsNoTracking().FirstOrNotFoundAsync(x => x.Id == req.PatientId, "Patient was not found.", ct);

        var visits = await db.Visits.AsNoTracking()
            .Where(x => x.PatientId == req.PatientId)
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
                x.Status))
            .ToListAsync(ct);

        await Send.OkAsync(new Response(visits), ct);
    }
}

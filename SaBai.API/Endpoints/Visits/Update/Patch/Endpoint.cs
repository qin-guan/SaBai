using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Visits.Update.Patch;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Patch("/visits/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Update a visit");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var visit = await db.Visits.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Visit was not found.",
            ct
        );

        if (req.PatientId is int patientId)
        {
            await db
                .Patients.AsNoTracking()
                .FirstOrNotFoundAsync(x => x.Id == patientId, "Patient was not found.", ct);
            visit.PatientId = patientId;
        }

        if (req.VillageCodeId is int villageCodeId)
        {
            await db
                .VillageCodes.AsNoTracking()
                .FirstOrNotFoundAsync(
                    x => x.Id == villageCodeId,
                    "Village code was not found.",
                    ct
                );
            visit.VillageCodeId = villageCodeId;
        }

        if (req.Date is DateTimeOffset date)
        {
            visit.Date = date;
        }

        if (req.Status is not null)
        {
            visit.Status = req.Status.Trim();
        }

        await db.SaveChangesAsync(ct);

        var response = await db
            .Visits.AsNoTracking()
            .Where(x => x.Id == visit.Id)
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

        await Send.OkAsync(response, ct);
    }
}

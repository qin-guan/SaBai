using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Visits.Create.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/visits");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Create a visit");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var patient = await db
            .Patients.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.PatientId, "Patient was not found.", ct);
        var village = await db
            .VillageCodes.AsNoTracking()
            .FirstOrNotFoundAsync(
                x => x.Id == req.VillageCodeId,
                "Village code was not found.",
                ct
            );

        var visit = await db
            .Visits.Include(x => x.VillageCode)
            .FindExistingAsync(req.Id, x => x.PatientId == req.PatientId, ct);
        if (visit is not null)
        {
            // A resent create: answer with the visit as it is now.
            village = visit.VillageCode;
        }
        else
        {
            var now = DateTimeOffset.UtcNow;
            visit = new Visit
            {
                Id = req.Id ?? Guid.CreateVersion7(),
                PatientId = patient.Id,
                VillageCodeId = village.Id,
                // A device sends the time it opened the visit offline; never later than now.
                Date = req.Date is { } date && date < now ? date : now,
                Status = string.IsNullOrWhiteSpace(req.Status)
                    ? Visit.StartedStatus
                    : req.Status.Trim(),
            };
            db.Visits.Add(visit);
            await db.SaveChangesAsync(ct);
        }

        await Send.CreatedAtAsync<Get.Endpoint>(
            new { visit.Id },
            new Response(
                visit.Id,
                visit.Date,
                patient.Id,
                patient.Name,
                village.Id,
                village.Code,
                village.Name,
                village.ColorHex,
                visit.Status
            ),
            cancellation: ct
        );
    }
}

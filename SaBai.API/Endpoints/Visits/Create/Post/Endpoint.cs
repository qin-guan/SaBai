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

        var visit = new Visit
        {
            PatientId = patient.Id,
            VillageCodeId = village.Id,
            Date = req.Date ?? DateTimeOffset.UtcNow,
            Status = string.IsNullOrWhiteSpace(req.Status)
                ? Visit.StartedStatus
                : req.Status.Trim(),
        };
        db.Visits.Add(visit);
        await db.SaveChangesAsync(ct);

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

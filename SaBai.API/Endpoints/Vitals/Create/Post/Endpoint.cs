using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Vitals.Create.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/vitals");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Record vitals for a visit");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        _ = await db
            .Visits.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.VisitId, "Visit was not found.", ct);

        if (await db.Vitals.FindExistingAsync(req.Id, x => x.VisitId == req.VisitId, ct) is { } existing)
        {
            await Send.CreatedAtAsync<Get.Endpoint>(
                new { existing.Id },
                Response.From(existing),
                cancellation: ct
            );
            return;
        }

        if (await db.Vitals.AnyAsync(x => x.VisitId == req.VisitId, ct))
        {
            throw new ConflictException(
                "Vitals were already recorded for this visit, perhaps on another device. Open the visit's vitals to change them."
            );
        }

        var vitals = new global::SaBai.API.Domain.Vitals
        {
            Id = req.Id ?? Guid.CreateVersion7(),
            VisitId = req.VisitId,
            Height = req.Height,
            Weight = req.Weight,
            Temperature = req.Temperature,
            Systolic = req.Systolic,
            Diastolic = req.Diastolic,
            HeartRate = req.HeartRate,
            HemocueCount = req.HemocueCount,
            DiabetesMellitus = req.DiabetesMellitus,
            UrineTest = FreeText.Normalize(req.UrineTest),
            BloodGlucoseNonFasting = req.BloodGlucoseNonFasting,
            BloodGlucoseFasting = req.BloodGlucoseFasting,
            Hba1c = req.Hba1c,
            Others = FreeText.Normalize(req.Others),
        };
        db.Vitals.Add(vitals);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException
                    is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
            )
        {
            throw new ConflictException(
                "Vitals were already recorded for this visit, perhaps on another device. Open the visit's vitals to change them."
            );
        }

        await Send.CreatedAtAsync<Get.Endpoint>(
            new { vitals.Id },
            Response.From(vitals),
            cancellation: ct
        );
    }
}

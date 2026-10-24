using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.ChildVitals.Create.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/child-vitals");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Record child vitals");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        _ = await db.Vitals
            .AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.VitalsId, "Vitals were not found.", ct);

        if (await db.ChildVitals.AnyAsync(x => x.VitalsId == req.VitalsId, ct))
        {
            throw new ConflictException("Child vitals already exist for this vitals record.");
        }

        var childVitals = new global::SaBai.API.Domain.ChildVitals
        {
            VitalsId = req.VitalsId,
            GrossMotor = FreeText.Normalize(req.GrossMotor),
            RedReflex = FreeText.Normalize(req.RedReflex),
            Scoliosis = FreeText.Normalize(req.Scoliosis),
            Pallor = req.Pallor,
            OralCavity = FreeText.Normalize(req.OralCavity),
            Heart = FreeText.Normalize(req.Heart),
            Abdomen = FreeText.Normalize(req.Abdomen),
            Lungs = FreeText.Normalize(req.Lungs),
            HernialOrifices = FreeText.Normalize(req.HernialOrifices),
        };
        db.ChildVitals.Add(childVitals);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Child vitals already exist for this vitals record.");
        }

        await Send.CreatedAtAsync<Get.Endpoint>(new { childVitals.Id }, Response.From(childVitals), cancellation: ct);
    }
}

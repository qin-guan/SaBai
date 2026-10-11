using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Glasses.Create.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/glasses");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Record prescribed glasses for a visit");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        _ = await db.Visits
            .AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.VisitId, "Visit was not found.", ct);

        var eyesight = await db.Eyesights.FirstOrDefaultAsync(x => x.VisitId == req.VisitId, ct);
        if (eyesight is null)
        {
            eyesight = new global::SaBai.API.Domain.Eyesight
            {
                VisitId = req.VisitId,
                LeftPrescribedGlassesDegree = FreeText.Normalize(req.LeftGlassesDegree),
                RightPrescribedGlassesDegree = FreeText.Normalize(req.RightGlassesDegree),
                Comments = FreeText.Normalize(req.Notes),
            };
            db.Eyesights.Add(eyesight);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                db.ChangeTracker.Clear();
                eyesight = await db.Eyesights.FirstOrNotFoundAsync(x => x.VisitId == req.VisitId, "Glasses were not found.", ct);
                ApplyProvided(eyesight, req);
                await db.SaveChangesAsync(ct);
            }
        }
        else
        {
            ApplyProvided(eyesight, req);
            await db.SaveChangesAsync(ct);
        }

        await Send.CreatedAtAsync<Get.Endpoint>(new { eyesight.Id }, Response.From(eyesight), cancellation: ct);
    }

    private static void ApplyProvided(global::SaBai.API.Domain.Eyesight eyesight, Request req)
    {
        if (req.LeftGlassesDegree is not null)
        {
            eyesight.LeftPrescribedGlassesDegree = FreeText.Normalize(req.LeftGlassesDegree);
        }

        if (req.RightGlassesDegree is not null)
        {
            eyesight.RightPrescribedGlassesDegree = FreeText.Normalize(req.RightGlassesDegree);
        }

        if (req.Notes is not null)
        {
            eyesight.Comments = FreeText.Normalize(req.Notes);
        }
    }
}

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Eyesight.Create.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/eyesight");
        Roles(StaffRoles.OptometryWork);
        Summary(s => s.Summary = "Record eyesight for a visit");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var leftEyeDegree = EyesightRules.NormalizeAcuity(req.LeftEyeDegree);
        var rightEyeDegree = EyesightRules.NormalizeAcuity(req.RightEyeDegree);
        var leftEyePinhole = EyesightRules.NormalizePinhole(req.LeftEyePinhole);
        var rightEyePinhole = EyesightRules.NormalizePinhole(req.RightEyePinhole);

        _ = await db
            .Visits.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.VisitId, "Visit was not found.", ct);

        var existing = await db.Eyesights.FindExistingAsync(req.Id, x => x.VisitId == req.VisitId, ct);
        if (existing is null && await db.Eyesights.AnyAsync(x => x.VisitId == req.VisitId, ct))
        {
            // A resend of this same create may have been saved between the two reads above:
            // look again before calling it someone else's record.
            existing = await db.Eyesights.FindExistingAsync(req.Id, x => x.VisitId == req.VisitId, ct)
                ?? throw new ConflictException(
                    "Vision was already recorded for this visit, perhaps on another device. Open the visit's vision record to change it."
                );
        }

        if (existing is not null)
        {
            await Send.CreatedAtAsync<Get.Endpoint>(
                new { existing.Id },
                Response.From(existing),
                cancellation: ct
            );
            return;
        }

        var eyesight = new global::SaBai.API.Domain.Eyesight
        {
            Id = req.Id ?? Guid.CreateVersion7(),
            VisitId = req.VisitId,
            LeftEyeDegree = leftEyeDegree,
            RightEyeDegree = rightEyeDegree,
            LeftEyePinhole = leftEyePinhole,
            RightEyePinhole = rightEyePinhole,
            LeftAstigmatism = FreeText.Normalize(req.LeftAstigmatism),
            RightAstigmatism = FreeText.Normalize(req.RightAstigmatism),
            Comments = FreeText.Normalize(req.Comments),
            LeftPrescribedGlassesDegree = FreeText.Normalize(req.LeftPrescribedGlassesDegree),
            RightPrescribedGlassesDegree = FreeText.Normalize(req.RightPrescribedGlassesDegree),
        };
        db.Eyesights.Add(eyesight);
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
                "Vision was already recorded for this visit, perhaps on another device. Open the visit's vision record to change it."
            );
        }

        await Send.CreatedAtAsync<Get.Endpoint>(
            new { eyesight.Id },
            Response.From(eyesight),
            cancellation: ct
        );
    }
}

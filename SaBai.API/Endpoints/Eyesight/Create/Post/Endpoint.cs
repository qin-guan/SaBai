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
        Roles(StaffRoles.Admin, StaffRoles.Member);
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

        if (await db.Eyesights.AnyAsync(x => x.VisitId == req.VisitId, ct))
        {
            throw new ConflictException("Eyesight already exists for this visit.");
        }

        var eyesight = new global::SaBai.API.Domain.Eyesight
        {
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
            throw new ConflictException("Eyesight already exists for this visit.");
        }

        await Send.CreatedAtAsync<Get.Endpoint>(
            new { eyesight.Id },
            Response.From(eyesight),
            cancellation: ct
        );
    }
}

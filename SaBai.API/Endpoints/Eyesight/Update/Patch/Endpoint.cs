using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Eyesight.Update.Patch;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Patch("/eyesight/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Update eyesight");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var entity = await db.Eyesights.FirstOrNotFoundAsync(x => x.Id == req.Id, "Eyesight was not found.", ct);

        if (req.LeftEyeDegree is not null)
        {
            entity.LeftEyeDegree = EyesightRules.NormalizeAcuity(req.LeftEyeDegree);
        }

        if (req.RightEyeDegree is not null)
        {
            entity.RightEyeDegree = EyesightRules.NormalizeAcuity(req.RightEyeDegree);
        }

        if (req.LeftEyePinhole is not null)
        {
            entity.LeftEyePinhole = EyesightRules.NormalizePinhole(req.LeftEyePinhole);
        }

        if (req.RightEyePinhole is not null)
        {
            entity.RightEyePinhole = EyesightRules.NormalizePinhole(req.RightEyePinhole);
        }

        if (req.LeftAstigmatism is not null)
        {
            entity.LeftAstigmatism = FreeText.Normalize(req.LeftAstigmatism);
        }

        if (req.RightAstigmatism is not null)
        {
            entity.RightAstigmatism = FreeText.Normalize(req.RightAstigmatism);
        }

        if (req.Comments is not null)
        {
            entity.Comments = FreeText.Normalize(req.Comments);
        }

        if (req.LeftPrescribedGlassesDegree is not null)
        {
            entity.LeftPrescribedGlassesDegree = FreeText.Normalize(req.LeftPrescribedGlassesDegree);
        }

        if (req.RightPrescribedGlassesDegree is not null)
        {
            entity.RightPrescribedGlassesDegree = FreeText.Normalize(req.RightPrescribedGlassesDegree);
        }

        await db.SaveChangesAsync(ct);
        await Send.OkAsync(Response.From(entity), ct);
    }
}

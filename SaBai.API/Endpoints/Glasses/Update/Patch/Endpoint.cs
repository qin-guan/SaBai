using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Glasses.Update.Patch;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Patch("/glasses/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Update prescribed glasses");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var eyesight = await db.Eyesights.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Glasses were not found.",
            ct
        );

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

        await db.SaveChangesAsync(ct);
        await Send.OkAsync(Response.From(eyesight), ct);
    }
}

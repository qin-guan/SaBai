using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.ChildVitals.Update.Patch;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Patch("/child-vitals/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Update child vitals");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var entity = await db.ChildVitals.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Child vitals were not found.",
            ct
        );

        if (req.GrossMotor is not null)
        {
            entity.GrossMotor = FreeText.Normalize(req.GrossMotor);
        }

        if (req.RedReflex is not null)
        {
            entity.RedReflex = FreeText.Normalize(req.RedReflex);
        }

        if (req.Scoliosis is not null)
        {
            entity.Scoliosis = FreeText.Normalize(req.Scoliosis);
        }

        if (req.Pallor is { } pallor)
        {
            entity.Pallor = pallor;
        }

        if (req.OralCavity is not null)
        {
            entity.OralCavity = FreeText.Normalize(req.OralCavity);
        }

        if (req.Heart is not null)
        {
            entity.Heart = FreeText.Normalize(req.Heart);
        }

        if (req.Abdomen is not null)
        {
            entity.Abdomen = FreeText.Normalize(req.Abdomen);
        }

        if (req.Lungs is not null)
        {
            entity.Lungs = FreeText.Normalize(req.Lungs);
        }

        if (req.HernialOrifices is not null)
        {
            entity.HernialOrifices = FreeText.Normalize(req.HernialOrifices);
        }

        await db.SaveChangesAsync(ct);
        await Send.OkAsync(Response.From(entity), ct);
    }
}

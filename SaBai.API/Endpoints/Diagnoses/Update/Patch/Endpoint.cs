using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Diagnoses.Update.Patch;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Patch("/diagnoses/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Update a diagnosis");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var diagnosis = await db.Diagnoses.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Diagnosis was not found.",
            ct
        );
        if (req.Details is not null)
        {
            diagnosis.Details = req.Details.Trim();
        }

        if (req.Category is not null)
        {
            diagnosis.Category = req.Category;
        }

        await db.SaveChangesAsync(ct);
        await Send.OkAsync(
            new Response(diagnosis.Id, diagnosis.ConsultId, diagnosis.Details, diagnosis.Category),
            ct
        );
    }
}

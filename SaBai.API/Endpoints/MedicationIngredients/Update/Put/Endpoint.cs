using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.MedicationIngredients.Update.Put;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Put("/medication-ingredients/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Replace a medication active ingredient");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var ingredient = await db.MedicationActiveIngredients.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Medication ingredient was not found.",
            ct
        );

        ingredient.Name = req.Name.Trim();
        ingredient.UnitOfMeasurement = req.UnitOfMeasurement.Trim();
        ingredient.FallBelow = req.FallBelow;
        ingredient.Remarks = string.IsNullOrWhiteSpace(req.Remarks) ? null : req.Remarks.Trim();
        await db.SaveChangesAsync(ct);

        await Send.OkAsync(
            new Response(
                ingredient.Id,
                ingredient.Name,
                ingredient.UnitOfMeasurement,
                ingredient.FallBelow,
                ingredient.Remarks
            ),
            ct
        );
    }
}

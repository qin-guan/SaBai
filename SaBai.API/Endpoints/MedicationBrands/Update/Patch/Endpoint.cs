using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.MedicationBrands.Update.Patch;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Verbs(Http.PATCH);
        Routes("/medication-brands/{Id}", "/medication-brands");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Update a medication brand");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var brand = await db.MedicationBrands.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Medication brand was not found.",
            ct);
        await db.MedicationActiveIngredients.FirstOrNotFoundAsync(
            x => x.Id == req.ActiveIngredientId,
            "Medication ingredient was not found.",
            ct);

        if (req.Name is not null)
        {
            brand.Name = req.Name.Trim();
        }

        brand.ActiveIngredientId = req.ActiveIngredientId;
        brand.Remarks = req.Remarks.Trim();
        await db.SaveChangesAsync(ct);

        await Send.OkAsync(new Response(brand.Id, brand.Name, brand.ActiveIngredientId, brand.Remarks), ct);
    }
}

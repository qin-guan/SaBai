using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationIngredients.Delete.Delete;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/medication-ingredients/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Delete a medication active ingredient");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var ingredient = await db.MedicationActiveIngredients.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Medication ingredient was not found.",
            ct
        );

        if (await db.MedicationBrands.AnyAsync(x => x.ActiveIngredientId == ingredient.Id, ct))
        {
            throw new ConflictException("The ingredient is referenced by a medication brand.");
        }

        db.MedicationActiveIngredients.Remove(ingredient);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

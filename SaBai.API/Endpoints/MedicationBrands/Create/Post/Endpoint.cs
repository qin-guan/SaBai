using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationBrands.Create.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/medication-brands");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Create a medication brand");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await db.MedicationActiveIngredients.FirstOrNotFoundAsync(
            x => x.Id == req.ActiveIngredientId,
            "Medication ingredient was not found.",
            ct);

        var brand = new MedicationBrand
        {
            Name = req.Name.Trim(),
            ActiveIngredientId = req.ActiveIngredientId,
            Remarks = req.Remarks.Trim(),
        };
        db.MedicationBrands.Add(brand);
        await db.SaveChangesAsync(ct);

        await Send.CreatedAtAsync<List.Get.Endpoint>(
            new { },
            new Response(brand.Id, brand.Name, brand.ActiveIngredientId, brand.Remarks),
            cancellation: ct);
    }
}

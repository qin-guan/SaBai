using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationIngredients.Create.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/medication-ingredients");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Create a medication active ingredient");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var ingredient = new MedicationActiveIngredient
        {
            Name = req.Name.Trim(),
            UnitOfMeasurement = req.UnitOfMeasurement.Trim(),
            FallBelow = req.FallBelow,
            Remarks = string.IsNullOrWhiteSpace(req.Remarks) ? null : req.Remarks.Trim(),
        };
        db.MedicationActiveIngredients.Add(ingredient);
        await db.SaveChangesAsync(ct);

        var response = new Response(
            ingredient.Id,
            ingredient.Name,
            ingredient.UnitOfMeasurement,
            ingredient.FallBelow,
            ingredient.Remarks);
        await Send.CreatedAtAsync<List.Get.Endpoint>(new { }, response, cancellation: ct);
    }
}

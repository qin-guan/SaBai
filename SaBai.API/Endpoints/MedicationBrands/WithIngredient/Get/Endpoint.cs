using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.MedicationBrands.WithIngredient.Get;

public sealed class Endpoint(AppDbContext db) : EndpointWithoutRequest<List<Response>>
{
    public override void Configure()
    {
        Get("/medication-brands/with-ingredient");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "List medication brands with ingredient names");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var brands = await db
            .MedicationBrands.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new Response(x.Id, x.Name, x.ActiveIngredient.Name))
            .ToListAsync(ct);

        await Send.OkAsync(brands, ct);
    }
}

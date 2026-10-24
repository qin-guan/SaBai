using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.MedicationIngredients.List.Get;

public sealed class Endpoint(AppDbContext db) : EndpointWithoutRequest<List<Response>>
{
    public override void Configure()
    {
        Get("/medication-ingredients");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "List medication active ingredients");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var ingredients = await db.MedicationActiveIngredients
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new Response(x.Id, x.Name, x.UnitOfMeasurement, x.FallBelow, x.Remarks))
            .ToListAsync(ct);

        await Send.OkAsync(ingredients, ct);
    }
}

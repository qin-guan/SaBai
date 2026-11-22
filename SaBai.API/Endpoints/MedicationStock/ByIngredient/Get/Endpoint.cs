using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.ByIngredient.Get;

public sealed class Endpoint(AppDbContext db) : EndpointWithoutRequest<List<Response>>
{
    public override void Configure()
    {
        Get("/medication-stock/by-ingredient");
        Roles(StaffRoles.OrderWork);
        Summary(s => s.Summary = "Orderable quantity per active ingredient");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await db
            .MedicationStocks.AsNoTracking()
            .Where(x => x.StockStatus == StockStatus.Active && x.Quantity > 0 && x.Expiry >= now)
            .GroupBy(x => new
            {
                x.MedicationBrand.ActiveIngredientId,
                x.MedicationBrand.ActiveIngredient.Name,
                x.MedicationBrand.ActiveIngredient.UnitOfMeasurement,
            })
            .Select(g => new
            {
                g.Key.ActiveIngredientId,
                g.Key.Name,
                g.Key.UnitOfMeasurement,
                Quantity = g.Sum(x => x.Quantity),
            })
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        await Send.OkAsync(
            [.. rows.Select(x => new Response(x.ActiveIngredientId, x.Name, x.UnitOfMeasurement, x.Quantity))],
            ct
        );
    }
}

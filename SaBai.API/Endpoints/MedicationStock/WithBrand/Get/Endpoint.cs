using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.WithBrand.Get;

public sealed class Endpoint(AppDbContext db) : EndpointWithoutRequest<List<Response>>
{
    public override void Configure()
    {
        Get("/medication-stock/with-brand");
        Roles(StaffRoles.PharmacyWork);
        Summary(s => s.Summary = "List medication stock with brand and ingredient names");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var stocks = await db
            .MedicationStocks.AsNoTracking()
            .Where(x => x.Quantity != 0)
            .OrderByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.MedicationBrandId,
                x.Quantity,
                x.ReservedQuantity,
                x.Expiry,
                x.Location,
                x.StockStatus,
                x.Remarks,
                x.ParentStockId,
                BrandName = x.MedicationBrand.Name,
                IngredientName = x.MedicationBrand.ActiveIngredient.Name,
                IngredientId = x.MedicationBrand.ActiveIngredientId,
                x.MedicationBrand.ActiveIngredient.UnitOfMeasurement,
                x.MedicationBrand.ActiveIngredient.FallBelow,
            })
            .ToListAsync(ct);

        // Low stock compares what is still available (not reserved for pending orders).
        var activeTotals = stocks
            .Where(x => x.StockStatus == StockStatus.Active)
            .GroupBy(x => x.IngredientId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity - x.ReservedQuantity));

        await Send.OkAsync(
            stocks
                .Select(x => new Response(
                    x.Id,
                    x.MedicationBrandId,
                    x.Quantity,
                    x.ReservedQuantity,
                    x.Quantity - x.ReservedQuantity,
                    x.Expiry,
                    x.Location,
                    x.StockStatus,
                    x.Remarks,
                    x.ParentStockId,
                    x.BrandName,
                    x.IngredientName,
                    x.IngredientId,
                    x.UnitOfMeasurement,
                    x.FallBelow,
                    x.StockStatus == StockStatus.Active
                        && activeTotals.GetValueOrDefault(x.IngredientId) <= x.FallBelow
                ))
                .ToList(),
            ct
        );
    }
}

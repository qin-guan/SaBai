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
        Summary(s =>
            s.Summary = "Orderable quantity of every active ingredient (0 when none is available)"
        );
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await db
            .MedicationActiveIngredients.AsNoTracking()
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Select(x => new Response(
                x.Id,
                x.Name,
                x.UnitOfMeasurement,
                x.Brands.SelectMany(b => b.Stocks)
                    .Where(s => s.StockStatus == StockStatus.Active && s.Expiry >= now)
                    .Sum(s => s.Quantity - s.ReservedQuantity)
            ))
            .ToListAsync(ct);

        await Send.OkAsync(rows, ct);
    }
}

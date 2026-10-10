using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.MedicationStock.WithBrand.Get;

public sealed class Endpoint(AppDbContext db) : EndpointWithoutRequest<List<Response>>
{
    public override void Configure()
    {
        Get("/medication-stock/with-brand");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "List medication stock with brand and ingredient names");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var stocks = await db.MedicationStocks
            .AsNoTracking()
            .Where(x => x.Quantity != 0)
            .OrderByDescending(x => x.Id)
            .Select(x => new Response(
                x.Id,
                x.MedicationBrandId,
                x.Quantity,
                x.Expiry,
                x.Location,
                x.StockStatus,
                x.Remarks,
                x.MedicationBrand.Name,
                x.MedicationBrand.ActiveIngredient.Name))
            .ToListAsync(ct);

        await Send.OkAsync(stocks, ct);
    }
}

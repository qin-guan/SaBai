using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.MedicationStock.List.Get;

public sealed class Endpoint(AppDbContext db) : EndpointWithoutRequest<List<Response>>
{
    public override void Configure()
    {
        Get("/medication-stock");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "List medication stock excluding empty lots");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var stocks = await db
            .MedicationStocks.AsNoTracking()
            .Where(x => x.Quantity != 0)
            .OrderBy(x => x.Id)
            .Select(x => new Response(
                x.Id,
                x.MedicationBrandId,
                x.Quantity,
                x.Expiry,
                x.Location,
                x.StockStatus,
                x.Remarks
            ))
            .ToListAsync(ct);

        await Send.OkAsync(stocks, ct);
    }
}

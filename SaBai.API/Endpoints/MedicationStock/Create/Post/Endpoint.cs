using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.Create.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/medication-stock");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Create a medication stock lot");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await db.MedicationBrands.FirstOrNotFoundAsync(
            x => x.Id == req.MedicationBrandId,
            "Medication brand was not found.",
            ct);

        var stock = new SaBai.API.Domain.MedicationStock
        {
            MedicationBrandId = req.MedicationBrandId,
            Quantity = req.Quantity,
            Expiry = req.Expiry,
            Location = req.Location.Trim(),
            StockStatus = req.StockStatus,
            Remarks = string.IsNullOrWhiteSpace(req.Remarks) ? null : req.Remarks.Trim(),
        };
        db.MedicationStocks.Add(stock);
        await db.SaveChangesAsync(ct);

        await Send.CreatedAtAsync<List.Get.Endpoint>(
            new { },
            new Response(stock.Id, stock.MedicationBrandId, stock.Quantity, stock.Expiry, stock.Location, stock.StockStatus, stock.Remarks),
            cancellation: ct);
    }
}

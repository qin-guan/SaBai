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
        Roles(StaffRoles.PharmacyWork);
        Summary(s => s.Summary = "Create a medication stock lot");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await db.MedicationBrands.FirstOrNotFoundAsync(
            x => x.Id == req.MedicationBrandId,
            "Medication brand was not found.",
            ct
        );

        var stock = await db.MedicationStocks.FindExistingAsync(
            req.Id,
            x => x.MedicationBrandId == req.MedicationBrandId,
            ct
        );
        if (stock is null)
        {
            stock = SaBai.API.Domain.MedicationStock.Create(
                req.Id ?? Guid.CreateVersion7(),
                req.MedicationBrandId,
                req.Quantity,
                req.Expiry,
                req.Location.Trim(),
                req.StockStatus,
                string.IsNullOrWhiteSpace(req.Remarks) ? null : req.Remarks.Trim()
            );
            db.MedicationStocks.Add(stock);
            db.StockChanges.AddRange(
                StockChangeLog.ForCreate(stock, User.Id(), DateTimeOffset.UtcNow)
            );
            await db.SaveChangesAsync(ct);
        }

        await Send.CreatedAtAsync<List.Get.Endpoint>(
            new { },
            Response.From(stock),
            cancellation: ct
        );
    }
}

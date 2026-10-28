using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.Update.Patch;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Patch("/medication-stock/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Update stock location, status, or remarks");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var stock = await db.MedicationStocks.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Medication stock was not found.",
            ct
        );
        var before = StockChangeLog.Snapshot.Of(stock);

        if (!string.IsNullOrWhiteSpace(req.Location))
        {
            stock.Location = req.Location.Trim();
        }

        if (req.StockStatus is { } status)
        {
            stock.StockStatus = status;
        }

        stock.Remarks = string.IsNullOrWhiteSpace(req.Remarks) ? null : req.Remarks.Trim();
        db.StockChanges.AddRange(
            StockChangeLog.ForUpdate(before, stock, User.Id(), DateTimeOffset.UtcNow)
        );
        await db.SaveChangesAsync(ct);

        await Send.OkAsync(
            new Response(
                stock.Id,
                stock.MedicationBrandId,
                stock.Quantity,
                stock.Expiry,
                stock.Location,
                stock.StockStatus,
                stock.Remarks
            ),
            ct
        );
    }
}

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.Delete.Delete;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/medication-stock/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Delete a medication stock lot");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var stock = await db.MedicationStocks.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Medication stock was not found.",
            ct);

        if (await db.StockChanges.AnyAsync(x => x.StockId == stock.Id, ct))
        {
            throw new ConflictException("Medication stock has change history and cannot be deleted.");
        }

        db.MedicationStocks.Remove(stock);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

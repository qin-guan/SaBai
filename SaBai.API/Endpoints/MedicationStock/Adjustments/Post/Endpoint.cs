using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.Adjustments.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/medication-stock/{Id}/adjustments");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Adjust a stock lot quantity");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var stock = await db.MedicationStocks.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Medication stock was not found.",
            ct
        );

        // A resent adjustment finds the change row it recorded the first time.
        var applied = await db.StockChanges.FindExistingAsync(
            req.AdjustmentId,
            x => x.StockId == req.Id,
            ct
        );
        if (applied is not null)
        {
            await SendAsync(stock, ct);
            return;
        }

        var newQuantity = stock.Quantity + req.QuantityChange;
        if (newQuantity < 0)
        {
            throw new BadRequestException("Stock quantity cannot be negative.");
        }

        var before = StockChangeLog.Snapshot.Of(stock);
        stock.Quantity = newQuantity;
        if (req.Remarks is not null)
        {
            stock.Remarks = string.IsNullOrWhiteSpace(req.Remarks) ? null : req.Remarks.Trim();
        }

        var changes = StockChangeLog
            .ForUpdate(before, stock, User.Id(), DateTimeOffset.UtcNow)
            .ToList();
        if (req.AdjustmentId is { } adjustmentId && changes.Count > 0)
        {
            // The quantity row stands for the adjustment; with no quantity change, the first row.
            var recorded =
                changes.FirstOrDefault(x => x.Field == StockChangeField.Quantity) ?? changes[0];
            recorded.Id = adjustmentId;
        }

        db.StockChanges.AddRange(changes);
        await db.SaveChangesAsync(ct);
        await SendAsync(stock, ct);
    }

    private async Task SendAsync(SaBai.API.Domain.MedicationStock stock, CancellationToken ct)
    {
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

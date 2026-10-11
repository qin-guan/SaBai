using System.Globalization;
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
            ct);

        var newQuantity = stock.Quantity + req.QuantityChange;
        if (newQuantity < 0)
        {
            throw new BadRequestException("Stock quantity cannot be negative.");
        }

        var previous = stock.Quantity;
        stock.Quantity = newQuantity;
        if (req.Remarks is not null)
        {
            stock.Remarks = string.IsNullOrWhiteSpace(req.Remarks) ? null : req.Remarks.Trim();
        }

        db.StockChanges.Add(new StockChange
        {
            StockId = stock.Id,
            Field = StockChangeField.Quantity,
            PreviousValue = previous.ToString(CultureInfo.InvariantCulture),
            NewValue = newQuantity.ToString(CultureInfo.InvariantCulture),
            UserId = User.Id(),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        await Send.OkAsync(
            new Response(stock.Id, stock.MedicationBrandId, stock.Quantity, stock.Expiry, stock.Location, stock.StockStatus, stock.Remarks),
            ct);
    }
}

using System.Globalization;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationHistory.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, List<Response>>
{
    public override void Configure()
    {
        Get("/medication-history");
        Roles(StaffRoles.PharmacyWork);
        Summary(s => s.Summary = "List dispensed orders and stock quantity changes for a brand");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        if (req.BrandId is not { } brandId || brandId == Guid.Empty)
        {
            throw new BadRequestException("brandId is required.");
        }
        var quantityRemaining =
            await db
                .MedicationStocks.AsNoTracking()
                .Where(x => x.MedicationBrandId == brandId && x.StockStatus == StockStatus.Active)
                .SumAsync(x => (int?)x.Quantity, ct)
            ?? 0;

        // What dispensed orders took from this brand's batches (an order can span several brands).
        var orders = await db
            .OrderAllocations.AsNoTracking()
            .Where(x => x.Stock.MedicationBrandId == brandId && x.Order.Status == OrderStatus.Dispensed)
            .Select(x => new
            {
                Actor = x.Order.DispensedBy != null ? x.Order.DispensedBy.Nickname : null,
                Patient = x.Order.Consult.Visit.Patient.Name,
                x.Quantity,
                Date = x.Order.DispensedAt ?? x.Order.CreatedAt,
            })
            .ToListAsync(ct);

        var changes = await db
            .StockChanges.AsNoTracking()
            // Dispensing also writes quantity rows; those are listed above as orders.
            .Where(x =>
                x.Stock.MedicationBrandId == brandId
                && x.Field == StockChangeField.Quantity
                && x.OrderId == null
            )
            .Select(x => new
            {
                x.PreviousValue,
                x.NewValue,
                Actor = x.User.Nickname,
                x.CreatedAt,
            })
            .ToListAsync(ct);

        var history = orders
            .Select(x => new Response(
                "order",
                NameOrDash(x.Actor),
                NameOrDash(x.Patient),
                -x.Quantity,
                quantityRemaining,
                x.Date
            ))
            .Concat(
                changes.Select(x => new Response(
                    "adjustment",
                    NameOrDash(x.Actor),
                    "-",
                    ParseChange(x.NewValue) - ParseChange(x.PreviousValue),
                    quantityRemaining,
                    x.CreatedAt
                ))
            )
            .OrderByDescending(x => x.Date)
            .ThenBy(x => x.Kind)
            .ToList();

        await Send.OkAsync(history, ct);
    }

    private static string NameOrDash(string? name) => string.IsNullOrWhiteSpace(name) ? "-" : name;

    private static int ParseChange(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
}

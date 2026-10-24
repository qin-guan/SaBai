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
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "List approved dispenses and stock adjustments for a brand");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        if (req.BrandId is not > 0)
        {
            throw new BadRequestException("brandId is required.");
        }

        var brandId = req.BrandId.Value;
        var quantityRemaining = await db.MedicationStocks
            .AsNoTracking()
            .Where(x => x.MedicationBrandId == brandId && x.StockStatus == StockStatus.Active)
            .SumAsync(x => (int?)x.Quantity, ct) ?? 0;

        var orders = await db.Orders
            .AsNoTracking()
            .Where(x => x.MedicationBrandId == brandId && x.Status == OrderStatus.Approved)
            .Select(x => new
            {
                Actor = x.ApprovedBy != null ? x.ApprovedBy.Nickname : null,
                Patient = x.Consult.Visit.Patient.Name,
                x.Quantity,
                Date = x.ApprovedAt ?? x.CreatedAt,
            })
            .ToListAsync(ct);

        var changes = await db.StockChanges
            .AsNoTracking()
            .Where(x => x.Stock.MedicationBrandId == brandId && x.Field == StockChangeField.Quantity)
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
                x.Date))
            .Concat(changes.Select(x => new Response(
                "adjustment",
                NameOrDash(x.Actor),
                "-",
                ParseChange(x.NewValue) - ParseChange(x.PreviousValue),
                quantityRemaining,
                x.CreatedAt)))
            .OrderByDescending(x => x.Date)
            .ThenBy(x => x.Kind)
            .ToList();

        await Send.OkAsync(history, ct);
    }

    private static string NameOrDash(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "-" : name;

    private static int ParseChange(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}

using System.Globalization;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.Changes.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, List<Response>>
{
    public override void Configure()
    {
        Get("/medication-stock/{Id}/changes");
        Roles(StaffRoles.PharmacyWork);
        Summary(s =>
            s.Summary = "List a stock batch's change log, newest first: what was dispensed from it and to whom, adjustments, splits and edits"
        );
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var stock =
            await db
                .MedicationStocks.AsNoTracking()
                .Where(x => x.Id == req.Id)
                .Select(x => new
                {
                    x.ParentStockId,
                    // Entering the batch wrote its first rows, all at one time.
                    CreatedAt = x.Changes.Min(c => (DateTimeOffset?)c.CreatedAt),
                })
                .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Medication stock was not found.");

        var query = db.StockChanges.AsNoTracking().Where(x => x.StockId == req.Id);
        if (req.DispensedOnly == true)
        {
            query = query.Where(x => x.OrderId != null);
        }

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Field,
                x.PreviousValue,
                x.NewValue,
                x.UserId,
                x.User.Nickname,
                x.User.UserName,
                x.CreatedAt,
                Dispensing = x.Order == null
                    ? null
                    : new
                    {
                        x.Order.Id,
                        x.Order.Quantity,
                        x.Order.ConsultId,
                        x.Order.Consult.VisitId,
                        VisitDate = x.Order.Consult.Visit.Date,
                        x.Order.Consult.Visit.PatientId,
                        PatientName = x.Order.Consult.Visit.Patient.Name,
                        PatientNumber = x.Order.Consult.Visit.Patient.Number,
                        VillageCode = x.Order.Consult.Visit.Patient.VillageCode != null
                            ? x.Order.Consult.Visit.Patient.VillageCode.Code
                            : null,
                    },
            })
            .ToListAsync(ct);

        // A split writes the parent's quantity row and the new batches' first rows at one time.
        var splitTimes = new HashSet<DateTimeOffset>();
        if (rows.Any(x => x.Field == StockChangeField.Quantity && x.Dispensing is null))
        {
            splitTimes =
            [
                .. await db
                    .MedicationStocks.AsNoTracking()
                    .Where(x => x.ParentStockId == req.Id)
                    .Select(x => x.Changes.Min(c => (DateTimeOffset?)c.CreatedAt))
                    .Where(x => x != null)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToListAsync(ct),
            ];
        }

        var response = rows.Select(x =>
            {
                var isQuantity = x.Field == StockChangeField.Quantity;
                int? after = isQuantity ? Parse(x.NewValue) : null;
                int? change = isQuantity ? Parse(x.NewValue) - Parse(x.PreviousValue) : null;
                StockChangeKind kind;
                if (x.Dispensing is not null)
                {
                    kind = StockChangeKind.Dispensed;
                }
                else if (x.CreatedAt == stock.CreatedAt && IsFirstValue(x.Field, x.PreviousValue))
                {
                    kind = stock.ParentStockId is null
                        ? StockChangeKind.Created
                        : StockChangeKind.SplitIn;
                }
                else if (isQuantity && change < 0 && splitTimes.Contains(x.CreatedAt))
                {
                    kind = StockChangeKind.SplitOut;
                }
                else
                {
                    kind = isQuantity ? StockChangeKind.Adjustment : StockChangeKind.Edit;
                }

                return new Response(
                    x.Id,
                    req.Id,
                    kind,
                    x.Field,
                    x.PreviousValue,
                    x.NewValue,
                    change,
                    after,
                    x.UserId,
                    !string.IsNullOrWhiteSpace(x.Nickname) ? x.Nickname : x.UserName ?? "-",
                    x.CreatedAt,
                    x.Dispensing is { } d
                        ? new DispensingDetails(
                            d.Id,
                            d.Quantity,
                            d.ConsultId,
                            d.VisitId,
                            d.VisitDate,
                            d.PatientId,
                            d.PatientName,
                            Patient.FormatDisplayId(d.VillageCode, d.PatientNumber)
                        )
                        : null
                );
            })
            .ToList();

        await Send.OkAsync(response, ct);
    }

    /// <summary>How <see cref="StockChangeLog.ForCreate"/> writes a new batch's rows: from nothing.</summary>
    private static bool IsFirstValue(StockChangeField field, string previous) =>
        field == StockChangeField.Quantity ? previous == "0" : previous.Length == 0;

    private static int Parse(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
}

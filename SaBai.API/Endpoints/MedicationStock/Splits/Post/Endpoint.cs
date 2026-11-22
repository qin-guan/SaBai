using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.Splits.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/medication-stock/{Id}/splits");
        Roles(StaffRoles.PharmacyWork);
        Summary(s => s.Summary = "Split a stock batch's unreserved units into new batches");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var response = await SplitAsync(req, ct);
        await Send.OkAsync(response, ct);
    }

    private Task<Response> SplitAsync(Request req, CancellationToken ct) =>
        OrderLifecycle.RunAsync(
            db,
            async token =>
            {
                var parent = await db.MedicationStocks.FirstOrNotFoundAsync(
                    x => x.Id == req.Id,
                    "Medication stock was not found.",
                    token
                );

                if (await IsReplayAsync(req, parent, token))
                {
                    return new Response(true);
                }

                if (parent.Quantity == 0)
                {
                    throw new ConflictException("The parent stock has already split.");
                }

                if (parent.AvailableQuantity == 0)
                {
                    throw new ConflictException(
                        "All of this stock is reserved for pending orders, so none of it can be split off."
                    );
                }

                var children = req
                    .Splits.Select(split => new
                    {
                        split.Id,
                        split.Quantity,
                        Location = split.Location.Trim(),
                        split.StockStatus,
                        Remarks = string.IsNullOrWhiteSpace(split.Remarks)
                            ? null
                            : split.Remarks.Trim(),
                    })
                    .ToList();

                // Every available (unreserved) unit moves into the children; reserved units stay
                // in the parent with the orders that hold them.
                if (children.Sum(x => x.Quantity) != parent.AvailableQuantity)
                {
                    throw new ConflictException(
                        parent.ReservedQuantity == 0
                            ? "Child stock quantity does not equal parent stock quantity!"
                            : $"Child stock quantity must equal the parent's {parent.AvailableQuantity} unreserved units; "
                                + $"{parent.ReservedQuantity} are reserved for pending orders."
                    );
                }

                var distinct = children
                    .Select(x =>
                        $"{x.Location}\u001f{x.StockStatus}\u001f{x.Quantity}\u001f{x.Remarks ?? ""}"
                    )
                    .Distinct(StringComparer.Ordinal)
                    .Count();
                if (distinct != children.Count)
                {
                    throw new ConflictException("Splits are not distinct!");
                }

                var userId = User.Id();
                var now = DateTimeOffset.UtcNow;
                foreach (var child in children)
                {
                    var stock = SaBai.API.Domain.MedicationStock.Create(
                        child.Id ?? Guid.CreateVersion7(),
                        parent.MedicationBrandId,
                        child.Quantity,
                        parent.Expiry,
                        child.Location,
                        child.StockStatus,
                        child.Remarks,
                        parentStockId: parent.Id
                    );
                    db.MedicationStocks.Add(stock);
                    db.StockChanges.AddRange(StockChangeLog.ForCreate(stock, userId, now));
                }

                var before = StockChangeLog.Snapshot.Of(parent);
                parent.SplitOff(parent.AvailableQuantity);
                db.StockChanges.AddRange(StockChangeLog.ForUpdate(before, parent, userId, now));
                return new Response(true);
            },
            ct
        );

    /// <summary>
    /// True when the request names an id for every child batch and those batches all exist and
    /// were split off this parent: the same split was already made. Throws when only some of the
    /// ids exist or they belong to another batch.
    /// </summary>
    private async Task<bool> IsReplayAsync(
        Request req,
        SaBai.API.Domain.MedicationStock parent,
        CancellationToken ct
    )
    {
        var ids = req.Splits.Select(x => x.Id).OfType<Guid>().ToList();
        if (ids.Count == 0)
        {
            return false;
        }

        var parents = await db
            .MedicationStocks.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => x.ParentStockId)
            .ToListAsync(ct);
        if (parents.Count == 0)
        {
            return false;
        }

        if (ids.Count == req.Splits.Count
            && parents.Count == ids.Count
            && parents.All(x => x == parent.Id))
        {
            return true;
        }

        throw new ConflictException("A split id is already used by another stock lot.");
    }
}

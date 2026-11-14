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
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Split a stock lot into child lots");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var response = await SplitAsync(req, ct);
        await Send.OkAsync(response, ct);
    }

    private async Task<Response> SplitAsync(Request req, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<Response>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var parent = await db.MedicationStocks.FirstOrNotFoundAsync(
                x => x.Id == req.Id,
                "Medication stock was not found.",
                ct
            );

            if (await IsReplayAsync(req, parent, ct))
            {
                return new Response(true);
            }

            if (parent.Quantity == 0)
            {
                throw new ConflictException("The parent stock has already split.");
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

            if (children.Sum(x => x.Quantity) != parent.Quantity)
            {
                throw new ConflictException(
                    "Child stock quantity does not equal parent stock quantity!"
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
                var stock = new SaBai.API.Domain.MedicationStock
                {
                    Id = child.Id ?? Guid.CreateVersion7(),
                    MedicationBrandId = parent.MedicationBrandId,
                    Quantity = child.Quantity,
                    Expiry = parent.Expiry,
                    Location = child.Location,
                    StockStatus = child.StockStatus,
                    Remarks = child.Remarks,
                };
                db.MedicationStocks.Add(stock);
                db.StockChanges.AddRange(StockChangeLog.ForCreate(stock, userId, now));
            }

            var before = StockChangeLog.Snapshot.Of(parent);
            parent.Quantity = 0;
            db.StockChanges.AddRange(StockChangeLog.ForUpdate(before, parent, userId, now));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new Response(true);
        });
    }

    /// <summary>
    /// True when the request names an id for every child lot and those lots all exist for the
    /// parent's brand: the same split was already made. Throws when only some of the ids exist
    /// or they belong to another brand.
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

        var brands = await db
            .MedicationStocks.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => x.MedicationBrandId)
            .ToListAsync(ct);
        if (brands.Count == 0)
        {
            return false;
        }

        if (ids.Count == req.Splits.Count
            && brands.Count == ids.Count
            && brands.All(x => x == parent.MedicationBrandId))
        {
            return true;
        }

        throw new ConflictException("A split id is already used by another stock lot.");
    }
}

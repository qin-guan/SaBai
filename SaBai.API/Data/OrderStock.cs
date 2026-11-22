using Microsoft.EntityFrameworkCore;
using SaBai.API.Domain;

namespace SaBai.API.Data;

/// <summary>
/// Moves stock lots through an order's life: set aside when the order is placed, put back if it
/// is cancelled, and handed out when it is approved. Callers run these inside a transaction.
/// </summary>
public static class OrderStock
{
    /// <summary>
    /// Reserves <see cref="Order.Quantity"/> of the order's active ingredient, taking from the
    /// lots that expire first (across brands, or only the order's brand when one is set). Whole
    /// lots are marked reserved; the last lot, if only partly needed, is split into a reserved
    /// part and an active remainder. The lots are locked while allocating, so when two orders
    /// compete for the same stock the first to commit wins and the other gets a conflict.
    /// </summary>
    /// <exception cref="ConflictException">Not enough unexpired, active stock.</exception>
    public static async Task ReserveAsync(
        AppDbContext db,
        Order order,
        Guid userId,
        DateTimeOffset at,
        CancellationToken ct
    )
    {
        var ingredientId = order.MedicationActiveIngredientId;
        var lots = order.MedicationBrandId is { } brandId
            ? await db
                .MedicationStocks.FromSqlInterpolated(
                    $"""
                    SELECT s.* FROM "MedicationStocks" s
                    JOIN "MedicationBrands" b ON b."Id" = s."MedicationBrandId"
                    WHERE s."StockStatus" = 'Active' AND s."Quantity" > 0 AND s."Expiry" >= {at}
                      AND b."ActiveIngredientId" = {ingredientId} AND b."Id" = {brandId}
                    ORDER BY s."Expiry", s."Id"
                    FOR UPDATE OF s
                    """
                )
                .ToListAsync(ct)
            : await db
                .MedicationStocks.FromSqlInterpolated(
                    $"""
                    SELECT s.* FROM "MedicationStocks" s
                    JOIN "MedicationBrands" b ON b."Id" = s."MedicationBrandId"
                    WHERE s."StockStatus" = 'Active' AND s."Quantity" > 0 AND s."Expiry" >= {at}
                      AND b."ActiveIngredientId" = {ingredientId}
                    ORDER BY s."Expiry", s."Id"
                    FOR UPDATE OF s
                    """
                )
                .ToListAsync(ct);

        var outstanding = order.Quantity;
        var whole = new List<MedicationStock>();
        MedicationStock? partial = null;
        var take = 0;
        foreach (var lot in lots)
        {
            if (outstanding == 0)
            {
                break;
            }

            if (lot.Quantity <= outstanding)
            {
                whole.Add(lot);
                outstanding -= lot.Quantity;
            }
            else
            {
                partial = lot;
                take = outstanding;
                outstanding = 0;
            }
        }

        if (outstanding > 0)
        {
            throw new ConflictException(
                $"Insufficient stock: {order.Quantity - outstanding} of {order.Quantity} available."
            );
        }

        foreach (var lot in whole)
        {
            var before = StockChangeLog.Snapshot.Of(lot);
            lot.StockStatus = StockStatus.Reserved;
            db.StockChanges.AddRange(StockChangeLog.ForUpdate(before, lot, userId, at));
            order.Items.Add(new OrderItem { Stock = lot, Quantity = lot.Quantity });
        }

        if (partial is not null)
        {
            var reserved = CopyOf(partial, take, StockStatus.Reserved);
            var remainder = CopyOf(partial, partial.Quantity - take, StockStatus.Active);
            var before = StockChangeLog.Snapshot.Of(partial);
            partial.Quantity = 0;
            db.MedicationStocks.AddRange(reserved, remainder);
            db.StockChanges.AddRange(StockChangeLog.ForUpdate(before, partial, userId, at));
            db.StockChanges.AddRange(StockChangeLog.ForCreate(reserved, userId, at));
            db.StockChanges.AddRange(StockChangeLog.ForCreate(remainder, userId, at));
            order.Items.Add(new OrderItem { Stock = reserved, Quantity = take });
        }
    }

    /// <summary>Puts an open order's reserved lots back into active stock and drops its items.</summary>
    public static void Release(AppDbContext db, Order order, Guid userId, DateTimeOffset at)
    {
        foreach (var item in order.Items)
        {
            var lot = item.Stock;
            if (lot.StockStatus != StockStatus.Reserved)
            {
                continue;
            }

            var before = StockChangeLog.Snapshot.Of(lot);
            lot.StockStatus = StockStatus.Active;
            db.StockChanges.AddRange(StockChangeLog.ForUpdate(before, lot, userId, at));
        }

        db.OrderItems.RemoveRange(order.Items);
        order.Items.Clear();
    }

    /// <summary>Hands out an order's reserved lots: each becomes dispensed with nothing left in it.</summary>
    public static void Dispense(AppDbContext db, Order order, Guid userId, DateTimeOffset at)
    {
        foreach (var item in order.Items)
        {
            var lot = item.Stock;
            var before = StockChangeLog.Snapshot.Of(lot);
            lot.Quantity = 0;
            lot.StockStatus = StockStatus.Dispensed;
            db.StockChanges.AddRange(StockChangeLog.ForUpdate(before, lot, userId, at));
        }
    }

    private static MedicationStock CopyOf(MedicationStock lot, int quantity, StockStatus status) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            MedicationBrandId = lot.MedicationBrandId,
            Quantity = quantity,
            Expiry = lot.Expiry,
            Location = lot.Location,
            StockStatus = status,
            Remarks = lot.Remarks,
        };
}

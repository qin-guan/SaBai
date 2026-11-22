using Microsoft.EntityFrameworkCore;
using SaBai.API.Domain;

namespace SaBai.API.Data;

/// <summary>
/// An order's life, from the doctor placing it to the pharmacist dispensing it, and what each step
/// does to the stock batches.
/// <list type="bullet">
/// <item><b>Place</b>: the quantity is reserved on active, unexpired batches of the ingredient
/// (any brand), the batch that expires first first; one <see cref="OrderAllocation"/> per batch.</item>
/// <item><b>Change</b> (pending only): new dosage, remarks, quantity or ingredient. A new quantity or
/// ingredient releases the reservations and allocates again.</item>
/// <item><b>Cancel</b> / <b>remove</b> (pending only): the reservations are released.</item>
/// <item><b>Dispense</b> (pending only): the reserved units leave each batch; the allocations stay as
/// the record of what was handed out.</item>
/// </list>
/// Every step runs through <see cref="RunAsync{T}"/>: in a transaction, retried when another request
/// changed one of the same batches first (their <see cref="MedicationStock.Version"/>), so two orders
/// racing for the last units can't both get them.
/// </summary>
public static class OrderLifecycle
{
    /// <summary>Attempts made when a batch was changed by someone else between read and save.</summary>
    public const int MaxAttempts = 5;

    /// <summary>
    /// Runs <paramref name="work"/> in a transaction on a clean change tracker and commits. When a
    /// stock batch it changed was saved by another request in the meantime
    /// (<see cref="DbUpdateConcurrencyException"/>), the whole unit runs again from a fresh read, up
    /// to <see cref="MaxAttempts"/> times; after that the exception is answered with 409.
    /// <paramref name="work"/> must load and build everything it saves, since each attempt starts
    /// with an empty change tracker.
    /// </summary>
    public static async Task<T> RunAsync<T>(
        AppDbContext db,
        Func<CancellationToken, Task<T>> work,
        CancellationToken ct
    )
    {
        var strategy = db.Database.CreateExecutionStrategy();
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await strategy.ExecuteAsync(async () =>
                {
                    db.ChangeTracker.Clear();
                    await using var transaction = await db.Database.BeginTransactionAsync(ct);
                    var result = await work(ct);
                    await db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    return result;
                });
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                db.ChangeTracker.Clear();
            }
        }
    }

    /// <inheritdoc cref="RunAsync{T}"/>
    public static Task RunAsync(AppDbContext db, Func<CancellationToken, Task> work, CancellationToken ct) =>
        RunAsync(
            db,
            async token =>
            {
                await work(token);
                return true;
            },
            ct
        );

    /// <summary>
    /// Adds a new pending order and reserves its quantity. Call inside <see cref="RunAsync{T}"/>.
    /// </summary>
    /// <exception cref="ConflictException">Not enough available stock of the ingredient.</exception>
    public static async Task PlaceAsync(AppDbContext db, Order order, CancellationToken ct)
    {
        order.Status = OrderStatus.Pending;
        db.Orders.Add(order);
        await AllocateAsync(db, order, ct);
    }

    /// <summary>
    /// Loads a pending order with its allocations and their batches, ready for
    /// <see cref="Change"/>, <see cref="Cancel"/>, <see cref="Remove"/> or <see cref="Dispense"/>.
    /// </summary>
    /// <exception cref="NotFoundException">No such order.</exception>
    public static Task<Order> LoadAsync(AppDbContext db, Guid orderId, CancellationToken ct) =>
        db
            .Orders.Include(x => x.Allocations)
            .ThenInclude(x => x.Stock)
            .FirstOrNotFoundAsync(x => x.Id == orderId, "Order was not found.", ct);

    /// <summary>
    /// Changes a pending order. When the quantity or the ingredient changes, its reservations are
    /// released and the new quantity is allocated again from scratch (so it may move to other
    /// batches); if that can't be covered nothing changes.
    /// </summary>
    /// <exception cref="BadRequestException">The order is no longer pending.</exception>
    /// <exception cref="ConflictException">Not enough available stock for the new quantity.</exception>
    public static async Task ChangeAsync(
        AppDbContext db,
        Order order,
        OrderChange change,
        CancellationToken ct
    )
    {
        EnsurePending(order);
        if (change.DosageInstructions is not null)
        {
            order.DosageInstructions = change.DosageInstructions.Trim();
        }

        if (change.Remarks is not null)
        {
            order.Remarks = string.IsNullOrWhiteSpace(change.Remarks) ? null : change.Remarks.Trim();
        }

        var quantity = change.Quantity ?? order.Quantity;
        var ingredientId = change.MedicationActiveIngredientId ?? order.MedicationActiveIngredientId;
        if (quantity == order.Quantity && ingredientId == order.MedicationActiveIngredientId)
        {
            return;
        }

        if (ingredientId != order.MedicationActiveIngredientId)
        {
            await db.MedicationActiveIngredients.FirstOrNotFoundAsync(
                x => x.Id == ingredientId,
                "Medication ingredient was not found.",
                ct
            );
        }

        order.Quantity = quantity;
        order.MedicationActiveIngredientId = ingredientId;
        await AllocateAsync(db, order, ct);
    }

    /// <summary>Cancels a pending order and releases its reservations.</summary>
    /// <exception cref="BadRequestException">The order is no longer pending.</exception>
    public static void Cancel(AppDbContext db, Order order)
    {
        EnsurePending(order);
        ReleaseAll(db, order);
        order.Status = OrderStatus.Cancelled;
    }

    /// <summary>
    /// Deletes an order that was never dispensed, releasing any reservations it holds. A dispensed
    /// order is the record of what was handed out and stays.
    /// </summary>
    /// <exception cref="ConflictException">The order was dispensed.</exception>
    public static void Remove(AppDbContext db, Order order)
    {
        if (order.Status == OrderStatus.Dispensed)
        {
            throw new ConflictException("A dispensed order cannot be deleted.");
        }

        ReleaseAll(db, order);
        db.Orders.Remove(order);
    }

    /// <summary>
    /// Marks a pending order dispensed: each allocated batch loses the allocated units from both
    /// its quantity on hand and its reservation, and a quantity change row is written for it.
    /// </summary>
    /// <exception cref="BadRequestException">The order is no longer pending.</exception>
    public static void Dispense(AppDbContext db, Order order, Guid userId, DateTimeOffset at)
    {
        EnsurePending(order);
        foreach (var allocation in order.Allocations)
        {
            var stock = allocation.Stock;
            var before = StockChangeLog.Snapshot.Of(stock);
            stock.Dispense(allocation.Quantity);
            foreach (var change in StockChangeLog.ForUpdate(before, stock, userId, at))
            {
                change.OrderId = order.Id;
                db.StockChanges.Add(change);
            }
        }

        order.Status = OrderStatus.Dispensed;
        order.DispensedById = userId;
        order.DispensedAt = at;
    }

    /// <exception cref="BadRequestException">The order was already dispensed or cancelled.</exception>
    public static void EnsurePending(Order order)
    {
        if (order.Status != OrderStatus.Pending)
        {
            throw new BadRequestException($"Order is already {OrderStatusName(order.Status)}.");
        }
    }

    /// <summary>The status as the API spells it (e.g. <c>DISPENSED</c>).</summary>
    public static string OrderStatusName(OrderStatus status) => status.ToString().ToUpperInvariant();

    /// <summary>
    /// Reserves <see cref="Order.Quantity"/> of the order's ingredient on the batches that expire
    /// first, across brands, replacing whatever the order held before. Batches already tracked
    /// (e.g. the order's own, or ones another order in this unit reserved) are used with their
    /// in-memory quantities, so what was just released or reserved counts.
    /// </summary>
    private static async Task AllocateAsync(AppDbContext db, Order order, CancellationToken ct)
    {
        var at = DateTimeOffset.UtcNow;
        foreach (var allocation in order.Allocations)
        {
            allocation.Stock.Release(allocation.Quantity);
        }

        // Filter on what the database holds; tracked batches keep their in-memory numbers, so the
        // available check runs again below.
        var batches = await db
            .MedicationStocks.Where(x =>
                x.MedicationBrand.ActiveIngredientId == order.MedicationActiveIngredientId
                && x.StockStatus == StockStatus.Active
                && x.Expiry >= at
                && x.Quantity > 0
            )
            .OrderBy(x => x.Expiry)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        var takes = new Dictionary<Guid, (MedicationStock Stock, int Units)>();
        var outstanding = order.Quantity;
        foreach (var batch in batches)
        {
            if (outstanding == 0)
            {
                break;
            }

            if (!batch.IsOrderable(at))
            {
                continue;
            }

            var units = Math.Min(batch.AvailableQuantity, outstanding);
            takes[batch.Id] = (batch, units);
            outstanding -= units;
        }

        if (outstanding > 0)
        {
            throw new ConflictException(
                $"Insufficient stock: {order.Quantity - outstanding} of {order.Quantity} available."
            );
        }

        // Keep an allocation row per batch (its key is order + batch): update the ones still used,
        // drop the others, add the new ones.
        foreach (var allocation in order.Allocations.ToList())
        {
            if (takes.Remove(allocation.StockId, out var take))
            {
                take.Stock.Reserve(take.Units);
                allocation.Quantity = take.Units;
            }
            else
            {
                order.Allocations.Remove(allocation);
                db.OrderAllocations.Remove(allocation);
            }
        }

        foreach (var (stock, units) in takes.Values)
        {
            stock.Reserve(units);
            order.Allocations.Add(
                new OrderAllocation
                {
                    OrderId = order.Id,
                    Order = order,
                    StockId = stock.Id,
                    Stock = stock,
                    Quantity = units,
                }
            );
        }
    }

    private static void ReleaseAll(AppDbContext db, Order order)
    {
        foreach (var allocation in order.Allocations)
        {
            allocation.Stock.Release(allocation.Quantity);
        }

        db.OrderAllocations.RemoveRange(order.Allocations);
        order.Allocations.Clear();
    }
}

/// <summary>What a change to a pending order asks for; null leaves a field as it is.</summary>
public sealed record OrderChange(
    string? DosageInstructions,
    string? Remarks,
    int? Quantity,
    Guid? MedicationActiveIngredientId
);

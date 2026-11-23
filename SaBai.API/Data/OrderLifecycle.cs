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
/// <item><b>Reallocate</b> (pending only): the pharmacist replaces the automatic choice of batches
/// with their own (<see cref="ReallocateAsync"/>): the old reservations are released and the chosen
/// batches reserved in the same save.</item>
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

    /// <summary>
    /// Replaces a pending order's allocations with the pharmacist's own choice of batches, e.g.
    /// when the batch allocated automatically can't be found or another box is already open. The
    /// order's reservations are released and <paramref name="picks"/> reserved instead; the units
    /// the order already holds on a batch count as available to it. Nothing changes unless every
    /// pick can be reserved. Reservations are not stock changes, so no <see cref="StockChange"/>
    /// rows are written here (dispensing writes them for the batches chosen); the allocation and
    /// batch rows are in the audit log like every other write.
    /// </summary>
    /// <exception cref="BadRequestException">
    /// The order is no longer pending, the picks don't add up to its quantity, or a batch is of
    /// another ingredient.
    /// </exception>
    /// <exception cref="NotFoundException">A picked batch doesn't exist.</exception>
    /// <exception cref="ConflictException">
    /// A picked batch is not active, has expired or has too few units available.
    /// </exception>
    public static async Task ReallocateAsync(
        AppDbContext db,
        Order order,
        IReadOnlyList<AllocationPick> picks,
        CancellationToken ct
    )
    {
        EnsurePending(order);
        if (picks.Count == 0)
        {
            throw new BadRequestException("Choose at least one stock batch.");
        }

        if (picks.Any(x => x.Quantity <= 0))
        {
            throw new BadRequestException("Each chosen batch needs a quantity of at least 1.");
        }

        if (picks.Select(x => x.StockId).Distinct().Count() != picks.Count)
        {
            throw new BadRequestException("Each stock batch can only be chosen once.");
        }

        var total = picks.Sum(x => (long)x.Quantity);
        if (total != order.Quantity)
        {
            throw new BadRequestException(
                $"The chosen batches add up to {total}, but the order is for {order.Quantity}."
            );
        }

        var current = order
            .Allocations.Select(x => (x.StockId, x.Quantity))
            .OrderBy(x => x.StockId)
            .ToList();
        if (current.SequenceEqual(picks.Select(x => (x.StockId, x.Quantity)).OrderBy(x => x.StockId)))
        {
            // Already allocated this way (e.g. the same request sent twice).
            return;
        }

        // The order's own batches are already tracked, so these come back as the same instances.
        var ids = picks.Select(x => x.StockId).ToList();
        var batches = await db
            .MedicationStocks.Include(x => x.MedicationBrand)
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        foreach (var allocation in order.Allocations)
        {
            allocation.Stock.Release(allocation.Quantity);
        }

        var at = DateTimeOffset.UtcNow;
        var takes = new Dictionary<Guid, (MedicationStock Stock, int Units)>();
        foreach (var pick in picks)
        {
            if (!batches.TryGetValue(pick.StockId, out var batch))
            {
                throw new NotFoundException($"Stock batch {pick.StockId} was not found.");
            }

            var label =
                $"{batch.MedicationBrand.Name} at {batch.Location} (expires {batch.Expiry.UtcDateTime:yyyy-MM-dd})";
            if (batch.MedicationBrand.ActiveIngredientId != order.MedicationActiveIngredientId)
            {
                throw new BadRequestException(
                    $"{label} is not a batch of the medication this order is for."
                );
            }

            if (batch.StockStatus != StockStatus.Active)
            {
                throw new ConflictException($"{label} is not active stock, so it can't be dispensed.");
            }

            if (batch.Expiry < at)
            {
                throw new ConflictException($"{label} has expired, so it can't be dispensed.");
            }

            if (pick.Quantity > batch.AvailableQuantity)
            {
                throw new ConflictException(
                    $"Only {batch.AvailableQuantity} of {label} are available, but {pick.Quantity} were chosen."
                );
            }

            takes[batch.Id] = (batch, pick.Quantity);
        }

        ApplyTakes(db, order, takes);
        // The order row itself has no changed column; mark it so UpdatedAt/UpdatedByUserId are
        // stamped and an offline write's If-Unmodified-Since check sees the reallocation.
        db.Entry(order).Property(x => x.UpdatedAt).IsModified = true;
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

        ApplyTakes(db, order, takes);
    }

    /// <summary>
    /// Makes the order's allocations match <paramref name="takes"/> and reserves the units. The
    /// order's previous reservations must already be released. Keeps an allocation row per batch
    /// (its key is order + batch): updates the ones still used, drops the others, adds the new ones.
    /// </summary>
    private static void ApplyTakes(
        AppDbContext db,
        Order order,
        Dictionary<Guid, (MedicationStock Stock, int Units)> takes
    )
    {
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

/// <summary>Units of one stock batch a pharmacist chose for an order.</summary>
public sealed record AllocationPick(Guid StockId, int Quantity);

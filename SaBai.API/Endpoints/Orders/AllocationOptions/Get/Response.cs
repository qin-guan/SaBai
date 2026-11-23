using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Orders.AllocationOptions.Get;

/// <summary>
/// A pending order and the stock batches of its active ingredient (any brand) it could be
/// dispensed from, the batch that expires first first. Send the pharmacist's choice to
/// <c>PUT /orders/{id}/allocations</c>, or as <c>allocations</c> when dispensing with
/// <c>PATCH /orders/{id}</c>.
/// </summary>
public sealed record Response(
    Guid OrderId,
    OrderStatus Status,
    Guid MedicationActiveIngredientId,
    string ActiveIngredientName,
    string UnitOfMeasurement,
    int Quantity,
    IReadOnlyList<BatchOption> Batches
);

/// <summary>
/// An active, non-empty batch of the order's ingredient. <c>AllocatedQuantity</c> is what this
/// order currently holds on it (0 when none). <c>AvailableQuantity</c> is what nobody has reserved
/// (as in the stock lists). <c>MaxQuantity</c> is the most this order may take from the batch:
/// the available units plus the ones it already holds, or 0 when the batch has expired
/// (<c>IsExpired</c>; an expired batch is only listed while the order still holds units on it).
/// </summary>
public sealed record BatchOption(
    Guid StockId,
    Guid MedicationBrandId,
    string BrandName,
    string Location,
    DateTimeOffset Expiry,
    bool IsExpired,
    int Quantity,
    int ReservedQuantity,
    int AvailableQuantity,
    int AllocatedQuantity,
    int MaxQuantity,
    string? Remarks
);

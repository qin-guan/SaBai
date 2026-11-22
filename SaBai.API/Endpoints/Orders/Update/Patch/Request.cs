using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Orders.Update.Patch;

/// <summary>
/// Change a pending order, and/or move it on: <c>DISPENSED</c> (the pharmacist handed it out) or
/// <c>CANCELLED</c>. Omitted fields stay as they are. A new quantity or ingredient re-allocates
/// the stock (409 and nothing changes when it can't be covered). Dispensed and cancelled orders
/// can't be changed.
/// </summary>
public sealed record Request
{
    public Guid Id { get; init; }

    /// <summary><c>DISPENSED</c> or <c>CANCELLED</c>; omit (or <c>PENDING</c>) to only change fields.</summary>
    public OrderStatus? Status { get; init; }

    public string? DosageInstructions { get; init; }

    /// <summary>Blank clears the remarks.</summary>
    public string? Remarks { get; init; }

    public int? Quantity { get; init; }

    public Guid? MedicationActiveIngredientId { get; init; }
}

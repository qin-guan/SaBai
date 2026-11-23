namespace SaBai.API.Endpoints.Orders.Allocations.Put;

/// <summary>
/// The pharmacist's own choice of stock batches for a pending order, replacing the batches that
/// were allocated automatically. The quantities must add up to the order's quantity. Every batch
/// must be of the order's active ingredient, active, not expired and have that many units
/// available; units this order already holds on a batch count as available to it. See
/// <c>GET /orders/{id}/allocation-options</c> for the batches that can be chosen.
/// </summary>
public sealed record Request
{
    public Guid Id { get; init; }

    public IReadOnlyList<AllocationChoice> Allocations { get; init; } = [];
}

namespace SaBai.API.Endpoints.Orders.Delete.Delete;

/// <summary>
/// Identifies the medication order to delete. A pending order's reserved stock is released; a
/// dispensed order can't be deleted.
/// </summary>
public sealed record Request
{
    public Guid Id { get; init; }
}

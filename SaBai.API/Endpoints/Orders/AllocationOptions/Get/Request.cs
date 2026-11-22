namespace SaBai.API.Endpoints.Orders.AllocationOptions.Get;

/// <summary>Identifies the pending order whose candidate stock batches should be listed.</summary>
public sealed record Request
{
    public Guid Id { get; init; }
}

namespace SaBai.API.Endpoints.Orders.List.Get;

/// <summary>Optional order status filter. Accepts PENDING, DISPENSED, or CANCELLED.</summary>
public sealed record Request
{
    public string? Status { get; init; }
}

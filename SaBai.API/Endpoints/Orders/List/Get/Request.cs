namespace SaBai.API.Endpoints.Orders.List.Get;

/// <summary>Optional order status filter. Accepts PENDING, APPROVED, or CANCELLED.</summary>
public sealed record Request
{
    public string? Status { get; init; }
}

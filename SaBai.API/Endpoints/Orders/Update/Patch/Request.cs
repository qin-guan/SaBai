using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Orders.Update.Patch;

/// <summary>Status transition for a pending order. Notes may be updated with the status.</summary>
public sealed record Request
{
    public int Id { get; init; }

    public OrderStatus? Status { get; init; }

    public string? DosageInstructions { get; init; }

    public string? Remarks { get; init; }
}

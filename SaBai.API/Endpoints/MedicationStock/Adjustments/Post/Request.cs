namespace SaBai.API.Endpoints.MedicationStock.Adjustments.Post;

/// <summary>Catalog quantity adjustment. The change may be negative.</summary>
public sealed record Request
{
    public Guid Id { get; init; }

    /// <summary>
    /// Id for the stock-change row this adjustment records, chosen by the client; generated when
    /// omitted. Resending an adjustment with the same id returns the lot as it is now and does
    /// not apply the change again.
    /// </summary>
    public Guid? AdjustmentId { get; init; }

    public int QuantityChange { get; init; }

    public string? Remarks { get; init; }
}

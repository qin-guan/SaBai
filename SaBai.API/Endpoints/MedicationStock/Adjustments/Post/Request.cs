namespace SaBai.API.Endpoints.MedicationStock.Adjustments.Post;

/// <summary>Catalog quantity adjustment. The change may be negative.</summary>
public sealed record Request
{
    public int Id { get; init; }

    public int QuantityChange { get; init; }

    public string? Remarks { get; init; }
}

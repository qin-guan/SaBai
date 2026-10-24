using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.Create.Post;

/// <summary>New stock lot. Quantity may be zero.</summary>
public sealed record Request
{
    public int MedicationBrandId { get; init; }

    public int Quantity { get; init; }

    public DateTimeOffset Expiry { get; init; }

    public required string Location { get; init; }

    public StockStatus StockStatus { get; init; }

    public string? Remarks { get; init; }
}

using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.Create.Post;

/// <summary>New stock lot. Quantity may be zero.</summary>
public sealed record Request
{
    /// <summary>Id for the new record, chosen by the client; generated when omitted. Resending a create with the same id returns the existing record.</summary>
    public Guid? Id { get; init; }

    public Guid MedicationBrandId { get; init; }

    public int Quantity { get; init; }

    public DateTimeOffset Expiry { get; init; }

    public required string Location { get; init; }

    public StockStatus StockStatus { get; init; }

    public string? Remarks { get; init; }
}

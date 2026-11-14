namespace SaBai.API.Endpoints.Orders.Create.Post;

/// <summary>Pending medication order. Creating an order does not change stock.</summary>
public sealed record Request
{
    /// <summary>Id for the new record, chosen by the client; generated when omitted. Resending a create with the same id returns the existing record.</summary>
    public Guid? Id { get; init; }

    public Guid ConsultId { get; init; }

    public Guid MedicationBrandId { get; init; }

    public int Quantity { get; init; }

    public required string DosageInstructions { get; init; }

    public string? Remarks { get; init; }
}

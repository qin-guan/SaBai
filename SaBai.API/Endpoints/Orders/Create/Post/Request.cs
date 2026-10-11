namespace SaBai.API.Endpoints.Orders.Create.Post;

/// <summary>Pending medication order. Creating an order does not change stock.</summary>
public sealed record Request
{
    public int ConsultId { get; init; }

    public int MedicationBrandId { get; init; }

    public int Quantity { get; init; }

    public required string DosageInstructions { get; init; }

    public string? Remarks { get; init; }
}

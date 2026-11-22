namespace SaBai.API.Endpoints.Orders.Create.Post;

/// <summary>
/// Pending medication order for an active ingredient. The stock is reserved immediately: the
/// batches that expire first are set aside (any brand), and the response lists which brand and
/// batch to take each part from. If there isn't enough available stock the order is refused (409).
/// Cancelling or deleting the order releases the stock.
/// </summary>
public sealed record Request
{
    /// <summary>Id for the new record, chosen by the client; generated when omitted. Resending a create with the same id returns the existing record.</summary>
    public Guid? Id { get; init; }

    public Guid ConsultId { get; init; }

    /// <summary>The active ingredient to order.</summary>
    public Guid MedicationActiveIngredientId { get; init; }

    public int Quantity { get; init; }

    public required string DosageInstructions { get; init; }

    public string? Remarks { get; init; }
}

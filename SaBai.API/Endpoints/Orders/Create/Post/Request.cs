namespace SaBai.API.Endpoints.Orders.Create.Post;

/// <summary>
/// Pending medication order for an active ingredient. The stock is reserved immediately: the
/// lots that expire first are set aside (any brand), and the response lists which brand and lot
/// to take each part from. If there isn't enough stock the order is refused. Cancelling the order
/// releases the stock.
/// </summary>
public sealed record Request
{
    /// <summary>Id for the new record, chosen by the client; generated when omitted. Resending a create with the same id returns the existing record.</summary>
    public Guid? Id { get; init; }

    public Guid ConsultId { get; init; }

    /// <summary>The active ingredient to order. Required unless <see cref="MedicationBrandId"/> is given.</summary>
    public Guid? MedicationActiveIngredientId { get; init; }

    /// <summary>
    /// Optional: restrict the order to one brand's stock. When given without an ingredient, the
    /// brand's ingredient is used. Leave empty to let the pharmacy use any brand.
    /// </summary>
    public Guid? MedicationBrandId { get; init; }

    public int Quantity { get; init; }

    public required string DosageInstructions { get; init; }

    public string? Remarks { get; init; }
}

namespace SaBai.API.Endpoints.MedicationBrands.Create.Post;

/// <summary>New medication brand. Remarks may be an empty string.</summary>
public sealed record Request
{
    public required string Name { get; init; }

    public int ActiveIngredientId { get; init; }

    public required string Remarks { get; init; }
}

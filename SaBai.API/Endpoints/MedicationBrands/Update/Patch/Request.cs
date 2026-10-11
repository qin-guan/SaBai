namespace SaBai.API.Endpoints.MedicationBrands.Update.Patch;

/// <summary>Partial brand update. Name may be omitted; remarks may be empty.</summary>
public sealed record Request
{
    public int Id { get; init; }

    public string? Name { get; init; }

    public int ActiveIngredientId { get; init; }

    public required string Remarks { get; init; }
}

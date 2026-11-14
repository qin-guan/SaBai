namespace SaBai.API.Endpoints.MedicationBrands.Update.Patch;

/// <summary>Partial brand update. Name may be omitted; remarks may be empty.</summary>
public sealed record Request
{
    public Guid Id { get; init; }

    public string? Name { get; init; }

    public Guid ActiveIngredientId { get; init; }

    public required string Remarks { get; init; }
}

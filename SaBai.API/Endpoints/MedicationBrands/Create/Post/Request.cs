namespace SaBai.API.Endpoints.MedicationBrands.Create.Post;

/// <summary>New medication brand. Remarks may be an empty string.</summary>
public sealed record Request
{
    /// <summary>Id for the new record, chosen by the client; generated when omitted. Resending a create with the same id returns the existing record.</summary>
    public Guid? Id { get; init; }

    public required string Name { get; init; }

    public Guid ActiveIngredientId { get; init; }

    public required string Remarks { get; init; }
}

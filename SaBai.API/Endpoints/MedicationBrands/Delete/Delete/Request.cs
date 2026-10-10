namespace SaBai.API.Endpoints.MedicationBrands.Delete.Delete;

/// <summary>Identifies the medication brand to delete.</summary>
public sealed record Request
{
    public int Id { get; init; }
}

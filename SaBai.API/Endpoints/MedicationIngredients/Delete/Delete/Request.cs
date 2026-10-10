namespace SaBai.API.Endpoints.MedicationIngredients.Delete.Delete;

/// <summary>Identifies the medication active ingredient to delete.</summary>
public sealed record Request
{
    public int Id { get; init; }
}

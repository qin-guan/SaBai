namespace SaBai.API.Endpoints.MedicationBrands.WithIngredient.Get;

/// <summary>Medication brand joined to its active ingredient name.</summary>
public sealed record Response(int Id, string Name, string ActiveIngredientName);

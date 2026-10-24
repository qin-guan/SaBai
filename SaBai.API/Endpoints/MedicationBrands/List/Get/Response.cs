namespace SaBai.API.Endpoints.MedicationBrands.List.Get;

/// <summary>Medication brand catalog row.</summary>
public sealed record Response(int Id, string Name, int ActiveIngredientId, string? Remarks);

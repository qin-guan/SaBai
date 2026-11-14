namespace SaBai.API.Endpoints.MedicationBrands.List.Get;

/// <summary>Medication brand catalog row.</summary>
public sealed record Response(Guid Id, string Name, Guid ActiveIngredientId, string? Remarks);

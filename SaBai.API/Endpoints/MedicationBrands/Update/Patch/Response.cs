namespace SaBai.API.Endpoints.MedicationBrands.Update.Patch;

/// <summary>Updated medication brand.</summary>
public sealed record Response(int Id, string Name, int ActiveIngredientId, string? Remarks);

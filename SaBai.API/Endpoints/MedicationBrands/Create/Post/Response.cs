namespace SaBai.API.Endpoints.MedicationBrands.Create.Post;

/// <summary>Created medication brand.</summary>
public sealed record Response(int Id, string Name, int ActiveIngredientId, string? Remarks);

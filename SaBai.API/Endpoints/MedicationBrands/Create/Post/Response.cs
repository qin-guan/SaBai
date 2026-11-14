namespace SaBai.API.Endpoints.MedicationBrands.Create.Post;

/// <summary>Created medication brand.</summary>
public sealed record Response(Guid Id, string Name, Guid ActiveIngredientId, string? Remarks);

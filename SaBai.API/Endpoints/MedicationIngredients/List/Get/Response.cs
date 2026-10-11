namespace SaBai.API.Endpoints.MedicationIngredients.List.Get;

/// <summary>Medication active ingredient in catalog order.</summary>
public sealed record Response(
    int Id,
    string Name,
    string UnitOfMeasurement,
    int FallBelow,
    string? Remarks);

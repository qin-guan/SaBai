namespace SaBai.API.Endpoints.MedicationStock.ByIngredient.Get;

/// <summary>
/// An active ingredient with the quantity that can still be ordered: the unreserved units of its
/// active, unexpired batches across all brands. Every ingredient is listed; one with nothing
/// available has 0 and can't be ordered (placing an order for it is refused with 409).
/// </summary>
public sealed record Response(
    Guid IngredientId,
    string IngredientName,
    string UnitOfMeasurement,
    int AvailableQuantity
);

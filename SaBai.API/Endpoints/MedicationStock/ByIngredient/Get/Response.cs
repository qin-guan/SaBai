namespace SaBai.API.Endpoints.MedicationStock.ByIngredient.Get;

/// <summary>
/// An active ingredient with the quantity that can still be ordered: active, unexpired stock
/// across all brands. Used to pick an ingredient when placing an order.
/// </summary>
public sealed record Response(
    Guid IngredientId,
    string IngredientName,
    string UnitOfMeasurement,
    int AvailableQuantity
);

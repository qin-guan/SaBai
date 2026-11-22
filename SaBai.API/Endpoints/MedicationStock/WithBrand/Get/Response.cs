using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.WithBrand.Get;

/// <summary>
/// Non-empty stock batch with brand and ingredient names. <c>ReservedQuantity</c> of its
/// <c>Quantity</c> is set aside for pending orders; <c>AvailableQuantity</c> is the rest.
/// <c>IsLowStock</c> flags an active batch whose ingredient's total available (unreserved) active
/// quantity is at or below the ingredient's <c>FallBelow</c>.
/// </summary>
public sealed record Response(
    Guid Id,
    Guid MedicationBrandId,
    int Quantity,
    int ReservedQuantity,
    int AvailableQuantity,
    DateTimeOffset Expiry,
    string Location,
    StockStatus StockStatus,
    string? Remarks,
    Guid? ParentStockId,
    string BrandName,
    string IngredientName,
    Guid IngredientId,
    string UnitOfMeasurement,
    int FallBelow,
    bool IsLowStock
);

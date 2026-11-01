using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.WithBrand.Get;

/// <summary>
/// Non-empty stock lot with brand and ingredient names. <c>IsLowStock</c> flags an active lot
/// whose ingredient's total active quantity is at or below the ingredient's <c>FallBelow</c>.
/// </summary>
public sealed record Response(
    int Id,
    int MedicationBrandId,
    int Quantity,
    DateTimeOffset Expiry,
    string Location,
    StockStatus StockStatus,
    string? Remarks,
    string BrandName,
    string IngredientName,
    int IngredientId,
    int FallBelow,
    bool IsLowStock
);

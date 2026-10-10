using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.WithBrand.Get;

/// <summary>Non-empty stock lot with brand and ingredient names.</summary>
public sealed record Response(
    int Id,
    int MedicationBrandId,
    int Quantity,
    DateTimeOffset Expiry,
    string Location,
    StockStatus StockStatus,
    string? Remarks,
    string BrandName,
    string IngredientName);

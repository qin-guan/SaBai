using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.List.Get;

/// <summary>Stock lot with a non-zero quantity. <c>UnitOfMeasurement</c> is its ingredient's unit, which the quantities count.</summary>
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
    string UnitOfMeasurement
);

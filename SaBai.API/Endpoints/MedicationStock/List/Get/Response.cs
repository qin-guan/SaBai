using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.List.Get;

/// <summary>Stock lot with a non-zero quantity.</summary>
public sealed record Response(
    int Id,
    int MedicationBrandId,
    int Quantity,
    DateTimeOffset Expiry,
    string Location,
    StockStatus StockStatus,
    string? Remarks);

using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.Adjustments.Post;

/// <summary>Stock lot after a catalog quantity adjustment.</summary>
public sealed record Response(
    int Id,
    int MedicationBrandId,
    int Quantity,
    DateTimeOffset Expiry,
    string Location,
    StockStatus StockStatus,
    string? Remarks
);

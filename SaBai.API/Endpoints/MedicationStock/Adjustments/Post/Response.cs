using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.Adjustments.Post;

/// <summary>Stock lot after a catalog quantity adjustment.</summary>
public sealed record Response(
    Guid Id,
    Guid MedicationBrandId,
    int Quantity,
    DateTimeOffset Expiry,
    string Location,
    StockStatus StockStatus,
    string? Remarks
);

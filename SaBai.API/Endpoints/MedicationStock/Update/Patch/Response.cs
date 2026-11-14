using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.Update.Patch;

/// <summary>Updated medication stock lot.</summary>
public sealed record Response(
    Guid Id,
    Guid MedicationBrandId,
    int Quantity,
    DateTimeOffset Expiry,
    string Location,
    StockStatus StockStatus,
    string? Remarks
);

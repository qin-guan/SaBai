using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.Create.Post;

/// <summary>Created medication stock lot.</summary>
public sealed record Response(
    int Id,
    int MedicationBrandId,
    int Quantity,
    DateTimeOffset Expiry,
    string Location,
    StockStatus StockStatus,
    string? Remarks
);

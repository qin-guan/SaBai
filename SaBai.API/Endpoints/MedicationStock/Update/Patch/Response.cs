using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.Update.Patch;

/// <summary>Updated medication stock lot.</summary>
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
    Guid? ParentStockId
)
{
    public static Response From(SaBai.API.Domain.MedicationStock x) =>
        new(
            x.Id,
            x.MedicationBrandId,
            x.Quantity,
            x.ReservedQuantity,
            x.AvailableQuantity,
            x.Expiry,
            x.Location,
            x.StockStatus,
            x.Remarks,
            x.ParentStockId
        );
}

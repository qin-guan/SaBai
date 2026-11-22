using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.Adjustments.Post;

/// <summary>Stock lot after a catalog quantity adjustment.</summary>
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

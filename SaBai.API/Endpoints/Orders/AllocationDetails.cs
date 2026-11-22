using System.Linq.Expressions;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Orders;

/// <summary>
/// Units of an order taken from one stock batch: the brand, batch and shelf location to take them
/// from, and how many. Assigned automatically (earliest expiry first) when the order is placed or
/// its quantity or ingredient changes. On a dispensed order it records what was handed out.
/// </summary>
public sealed record AllocationDetails(
    Guid StockId,
    Guid MedicationBrandId,
    string BrandName,
    string Location,
    DateTimeOffset Expiry,
    int Quantity
)
{
    public static readonly Expression<Func<OrderAllocation, AllocationDetails>> Projection =
        x => new AllocationDetails(
            x.StockId,
            x.Stock.MedicationBrandId,
            x.Stock.MedicationBrand.Name,
            x.Stock.Location,
            x.Stock.Expiry,
            x.Quantity
        );
}

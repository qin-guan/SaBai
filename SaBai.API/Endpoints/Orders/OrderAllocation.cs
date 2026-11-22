namespace SaBai.API.Endpoints.Orders;

/// <summary>
/// Where part of an order comes from: the brand and lot the pharmacist should take it from, and
/// how many. Assigned automatically (earliest expiry first) when the order is placed.
/// </summary>
public sealed record OrderAllocation(
    Guid StockId,
    Guid MedicationBrandId,
    string BrandName,
    string Location,
    DateTimeOffset Expiry,
    int Quantity
);

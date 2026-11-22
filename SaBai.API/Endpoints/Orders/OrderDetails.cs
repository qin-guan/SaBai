using System.Linq.Expressions;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Orders;

/// <summary>
/// An order as every order endpoint returns it. Each endpoint's response copies these fields, so
/// they all agree. <see cref="MedicationBrandId"/> is set only when the order asked for one brand.
/// </summary>
public sealed record OrderDetails(
    Guid Id,
    Guid ConsultId,
    Guid MedicationActiveIngredientId,
    string ActiveIngredientName,
    Guid? MedicationBrandId,
    string? BrandName,
    int Quantity,
    OrderStatus Status,
    string DosageInstructions,
    string? Remarks,
    Guid? ApprovedById,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderAllocation> Allocations
)
{
    public static readonly Expression<Func<Order, OrderDetails>> Projection = x => new OrderDetails(
        x.Id,
        x.ConsultId,
        x.MedicationActiveIngredientId,
        x.MedicationActiveIngredient.Name,
        x.MedicationBrandId,
        x.MedicationBrand != null ? x.MedicationBrand.Name : null,
        x.Quantity,
        x.Status,
        x.DosageInstructions,
        x.Remarks,
        x.ApprovedById,
        x.ApprovedAt,
        x.CreatedAt,
        x.Items.OrderBy(i => i.Stock.Expiry)
            .ThenBy(i => i.StockId)
            .Select(i => new OrderAllocation(
                i.StockId,
                i.Stock.MedicationBrandId,
                i.Stock.MedicationBrand.Name,
                i.Stock.Location,
                i.Stock.Expiry,
                i.Quantity
            ))
            .ToList()
    );
}

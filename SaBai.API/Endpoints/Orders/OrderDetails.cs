using System.Linq.Expressions;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Orders;

/// <summary>
/// An order as every order endpoint returns it. Each endpoint's response copies these fields, so
/// they all agree. Brand and batch details are in <see cref="Allocations"/>.
/// </summary>
public sealed record OrderDetails(
    Guid Id,
    Guid ConsultId,
    Guid MedicationActiveIngredientId,
    string ActiveIngredientName,
    int Quantity,
    OrderStatus Status,
    string DosageInstructions,
    string? Remarks,
    Guid? DispensedById,
    DateTimeOffset? DispensedAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AllocationDetails> Allocations
)
{
    public static readonly Expression<Func<Order, OrderDetails>> Projection = x => new OrderDetails(
        x.Id,
        x.ConsultId,
        x.MedicationActiveIngredientId,
        x.MedicationActiveIngredient.Name,
        x.Quantity,
        x.Status,
        x.DosageInstructions,
        x.Remarks,
        x.DispensedById,
        x.DispensedAt,
        x.CreatedAt,
        x.Allocations.AsQueryable()
            .OrderBy(i => i.Stock.Expiry)
            .ThenBy(i => i.StockId)
            .Select(AllocationDetails.Projection)
            .ToList()
    );
}

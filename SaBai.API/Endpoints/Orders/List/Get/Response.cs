using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Orders.List.Get;

/// <summary>
/// Medication order for an active ingredient, with the brands and lots the pharmacy takes it from.
/// </summary>
public sealed record Response(
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
    public static Response From(OrderDetails d) =>
        new(
            d.Id,
            d.ConsultId,
            d.MedicationActiveIngredientId,
            d.ActiveIngredientName,
            d.MedicationBrandId,
            d.BrandName,
            d.Quantity,
            d.Status,
            d.DosageInstructions,
            d.Remarks,
            d.ApprovedById,
            d.ApprovedAt,
            d.CreatedAt,
            d.Allocations
        );
};

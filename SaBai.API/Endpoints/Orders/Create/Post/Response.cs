using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Orders.Create.Post;

/// <summary>
/// Medication order for an active ingredient, with the brands and lots the pharmacy takes it from.
/// </summary>
public sealed record Response(
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
    public static Response From(OrderDetails d) =>
        new(
            d.Id,
            d.ConsultId,
            d.MedicationActiveIngredientId,
            d.ActiveIngredientName,
            d.Quantity,
            d.Status,
            d.DosageInstructions,
            d.Remarks,
            d.DispensedById,
            d.DispensedAt,
            d.CreatedAt,
            d.Allocations
        );
};

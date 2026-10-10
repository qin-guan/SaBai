using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Orders.Create.Post;

/// <summary>Medication order with its brand name.</summary>
public sealed record Response(
    int Id,
    int ConsultId,
    int MedicationBrandId,
    string BrandName,
    int Quantity,
    OrderStatus Status,
    string DosageInstructions,
    string? Remarks,
    Guid? ApprovedById,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset CreatedAt);

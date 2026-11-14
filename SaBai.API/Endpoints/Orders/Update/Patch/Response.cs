using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Orders.Update.Patch;

/// <summary>Medication order after a status or notes update.</summary>
public sealed record Response(
    Guid Id,
    Guid ConsultId,
    Guid MedicationBrandId,
    string BrandName,
    int Quantity,
    OrderStatus Status,
    string DosageInstructions,
    string? Remarks,
    Guid? ApprovedById,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset CreatedAt
);

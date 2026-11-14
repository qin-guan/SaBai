namespace SaBai.API.Endpoints.Visits.Update.Patch;

/// <summary>Partial visit update. Omitted fields are left unchanged.</summary>
public sealed record Request
{
    public Guid Id { get; init; }

    public Guid? PatientId { get; init; }

    public Guid? VillageCodeId { get; init; }

    public DateTimeOffset? Date { get; init; }

    public string? Status { get; init; }
}

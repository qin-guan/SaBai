namespace SaBai.API.Endpoints.Visits.Update.Patch;

/// <summary>Partial visit update. Omitted fields are left unchanged.</summary>
public sealed record Request
{
    public int Id { get; init; }

    public int? PatientId { get; init; }

    public int? VillageCodeId { get; init; }

    public DateTimeOffset? Date { get; init; }

    public string? Status { get; init; }
}

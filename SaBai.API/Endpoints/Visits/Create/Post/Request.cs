namespace SaBai.API.Endpoints.Visits.Create.Post;

/// <summary>Payload for opening a visit. Date and status default when omitted.</summary>
public sealed record Request
{
    /// <summary>Id for the new record, chosen by the client; generated when omitted. Resending a create with the same id returns the existing record.</summary>
    public Guid? Id { get; init; }

    public Guid PatientId { get; init; }

    public Guid VillageCodeId { get; init; }

    /// <summary>When the visit was opened; a time in the future is taken as now.</summary>
    public DateTimeOffset? Date { get; init; }

    public string? Status { get; init; }
}

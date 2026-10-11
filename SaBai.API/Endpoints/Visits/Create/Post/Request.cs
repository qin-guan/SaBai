namespace SaBai.API.Endpoints.Visits.Create.Post;

/// <summary>Payload for opening a visit. Date and status default when omitted.</summary>
public sealed record Request
{
    public int PatientId { get; init; }

    public int VillageCodeId { get; init; }

    public DateTimeOffset? Date { get; init; }

    public string? Status { get; init; }
}

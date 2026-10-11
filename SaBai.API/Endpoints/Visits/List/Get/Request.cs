namespace SaBai.API.Endpoints.Visits.List.Get;

/// <summary>Optional patient filter for the visit list.</summary>
public sealed record Request
{
    public int? PatientId { get; init; }
}

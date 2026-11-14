namespace SaBai.API.Endpoints.Visits.Get;

/// <summary>Identifies a visit.</summary>
public sealed record Request
{
    public Guid Id { get; init; }
}

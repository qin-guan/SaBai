namespace SaBai.API.Endpoints.Vitals.GetByVisit.Get;

/// <summary>Identifies the visit whose vitals are requested.</summary>
public sealed record Request
{
    public Guid VisitId { get; init; }
}

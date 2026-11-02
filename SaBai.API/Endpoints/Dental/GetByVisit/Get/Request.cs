namespace SaBai.API.Endpoints.Dental.GetByVisit.Get;

/// <summary>Identifies the visit whose dental consultation is requested.</summary>
public sealed record Request
{
    public int VisitId { get; init; }
}

namespace SaBai.API.Endpoints.Glasses.GetByVisit.Get;

/// <summary>Identifies the visit whose prescribed glasses are requested.</summary>
public sealed record Request
{
    public int VisitId { get; init; }
}

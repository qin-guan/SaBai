namespace SaBai.API.Endpoints.Eyesight.GetByVisit.Get;

/// <summary>Identifies the visit whose eyesight is requested.</summary>
public sealed record Request
{
    public int VisitId { get; init; }
}

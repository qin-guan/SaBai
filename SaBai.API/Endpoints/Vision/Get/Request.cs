namespace SaBai.API.Endpoints.Vision.Get;

/// <summary>Identifies the visit whose vision composite is requested.</summary>
public sealed record Request
{
    public int VisitId { get; init; }
}

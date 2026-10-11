namespace SaBai.API.Endpoints.Consults.ListByVisit.Get;

/// <summary>Visit whose consults should be listed.</summary>
public sealed record Request
{
    public int VisitId { get; init; }
}

namespace SaBai.API.Endpoints.Puberty.GetByVitals.Get;

/// <summary>Identifies the vitals record whose puberty assessment is requested.</summary>
public sealed record Request
{
    public int VitalsId { get; init; }
}

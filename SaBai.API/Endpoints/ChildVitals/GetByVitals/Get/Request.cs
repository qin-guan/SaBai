namespace SaBai.API.Endpoints.ChildVitals.GetByVitals.Get;

/// <summary>Identifies the vitals record whose child vitals are requested.</summary>
public sealed record Request
{
    public int VitalsId { get; init; }
}

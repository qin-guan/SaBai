namespace SaBai.API.Endpoints.Diagnoses.List.Get;

/// <summary>Optional consult filter for the diagnosis list.</summary>
public sealed record Request
{
    public int? ConsultId { get; init; }
}

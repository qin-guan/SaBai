namespace SaBai.API.Endpoints.Consults.Get;

/// <summary>Identifies a consult.</summary>
public sealed record Request
{
    public Guid Id { get; init; }
}

namespace SaBai.API.Endpoints.Puberty.Get;

/// <summary>Identifies a puberty assessment.</summary>
public sealed record Request
{
    public Guid Id { get; init; }
}

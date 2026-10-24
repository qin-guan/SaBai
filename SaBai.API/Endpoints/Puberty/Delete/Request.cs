namespace SaBai.API.Endpoints.Puberty.Delete;

/// <summary>Identifies a puberty assessment to delete.</summary>
public sealed record Request
{
    public int Id { get; init; }
}

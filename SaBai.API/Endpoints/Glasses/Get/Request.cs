namespace SaBai.API.Endpoints.Glasses.Get;

/// <summary>Identifies prescribed glasses by the eyesight record id.</summary>
public sealed record Request
{
    public int Id { get; init; }
}

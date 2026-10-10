namespace SaBai.API.Endpoints.Glasses.Update.Patch;

/// <summary>Partial prescribed-glasses update. Empty strings clear a field. The visit cannot be changed.</summary>
public sealed record Request
{
    public int Id { get; init; }

    public string? LeftGlassesDegree { get; init; }

    public string? RightGlassesDegree { get; init; }

    public string? Notes { get; init; }
}

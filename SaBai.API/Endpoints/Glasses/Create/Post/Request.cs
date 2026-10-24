namespace SaBai.API.Endpoints.Glasses.Create.Post;

/// <summary>Prescribed glasses degrees to record against a visit.</summary>
public sealed record Request
{
    public int VisitId { get; init; }

    public string? LeftGlassesDegree { get; init; }

    public string? RightGlassesDegree { get; init; }

    public string? Notes { get; init; }
}

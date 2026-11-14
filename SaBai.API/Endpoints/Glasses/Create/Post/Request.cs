namespace SaBai.API.Endpoints.Glasses.Create.Post;

/// <summary>Prescribed glasses degrees to record against a visit.</summary>
public sealed record Request
{
    /// <summary>Id for the eyesight record when this request creates it, chosen by the client; generated when omitted. Ignored when the visit already has one.</summary>
    public Guid? Id { get; init; }

    public Guid VisitId { get; init; }

    public string? LeftGlassesDegree { get; init; }

    public string? RightGlassesDegree { get; init; }

    public string? Notes { get; init; }
}

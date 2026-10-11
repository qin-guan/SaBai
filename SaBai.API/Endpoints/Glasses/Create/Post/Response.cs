namespace SaBai.API.Endpoints.Glasses.Create.Post;

/// <summary>Prescribed glasses degrees recorded against a visit.</summary>
public sealed record Response(
    int Id,
    int VisitId,
    string? LeftGlassesDegree,
    string? RightGlassesDegree,
    string? Notes)
{
    public static Response From(global::SaBai.API.Domain.Eyesight eyesight) => new(
        eyesight.Id,
        eyesight.VisitId,
        eyesight.LeftPrescribedGlassesDegree,
        eyesight.RightPrescribedGlassesDegree,
        eyesight.Comments);
}

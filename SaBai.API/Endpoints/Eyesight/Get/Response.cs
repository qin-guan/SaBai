namespace SaBai.API.Endpoints.Eyesight.Get;

/// <summary>Eyesight recorded for a visit.</summary>
public sealed record Response(
    int Id,
    int VisitId,
    string? LeftEyeDegree,
    string? RightEyeDegree,
    string? LeftEyePinhole,
    string? RightEyePinhole,
    string? LeftAstigmatism,
    string? RightAstigmatism,
    string? Comments,
    string? LeftPrescribedGlassesDegree,
    string? RightPrescribedGlassesDegree
)
{
    public static Response From(global::SaBai.API.Domain.Eyesight eyesight) =>
        new(
            eyesight.Id,
            eyesight.VisitId,
            eyesight.LeftEyeDegree,
            eyesight.RightEyeDegree,
            eyesight.LeftEyePinhole,
            eyesight.RightEyePinhole,
            eyesight.LeftAstigmatism,
            eyesight.RightAstigmatism,
            eyesight.Comments,
            eyesight.LeftPrescribedGlassesDegree,
            eyesight.RightPrescribedGlassesDegree
        );
}

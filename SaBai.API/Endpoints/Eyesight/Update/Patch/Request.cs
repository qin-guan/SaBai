namespace SaBai.API.Endpoints.Eyesight.Update.Patch;

/// <summary>Partial eyesight update. Omitted readings are left unchanged, and the visit cannot be changed.</summary>
public sealed record Request
{
    public int Id { get; init; }

    public string? LeftEyeDegree { get; init; }

    public string? RightEyeDegree { get; init; }

    public string? LeftEyePinhole { get; init; }

    public string? RightEyePinhole { get; init; }

    public string? LeftAstigmatism { get; init; }

    public string? RightAstigmatism { get; init; }

    public string? Comments { get; init; }

    public string? LeftPrescribedGlassesDegree { get; init; }

    public string? RightPrescribedGlassesDegree { get; init; }
}

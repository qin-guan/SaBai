namespace SaBai.API.Endpoints.Eyesight.Create.Post;

/// <summary>Eyesight readings to record against a visit.</summary>
public sealed record Request
{
    /// <summary>Id for the new record, chosen by the client; generated when omitted. Resending a create with the same id returns the existing record.</summary>
    public Guid? Id { get; init; }

    public Guid VisitId { get; init; }

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

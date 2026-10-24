namespace SaBai.API.Endpoints.Files.Create.Post;

/// <summary>Multipart upload of one or more clinical files for a patient.</summary>
public sealed record Request
{
    public int PatientId { get; init; }

    public List<IFormFile>? Files { get; init; }

    public List<string>? Descriptions { get; init; }
}

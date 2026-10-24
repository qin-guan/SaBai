namespace SaBai.API.Endpoints.Patients.Faces.Post;

/// <summary>Multipart enrollment of a face photo for a patient.</summary>
public sealed class Request
{
    public int Id { get; set; }

    public IFormFile Image { get; set; } = null!;
}

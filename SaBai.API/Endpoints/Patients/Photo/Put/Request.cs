namespace SaBai.API.Endpoints.Patients.Photo.Put;

/// <summary>Multipart upload replacing a patient's profile photo.</summary>
public sealed class Request
{
    public int Id { get; set; }

    public IFormFile Image { get; set; } = null!;
}

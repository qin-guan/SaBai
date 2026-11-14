namespace SaBai.API.Endpoints.Patients.Faces.Delete;

public sealed class Request
{
    public Guid Id { get; set; }

    public Guid FaceId { get; set; }
}

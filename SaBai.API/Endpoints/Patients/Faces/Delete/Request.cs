namespace SaBai.API.Endpoints.Patients.Faces.Delete;

public sealed class Request
{
    public int Id { get; set; }

    public Guid FaceId { get; set; }
}

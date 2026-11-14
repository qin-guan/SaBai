using SaBai.API.Endpoints.Faces;

namespace SaBai.API.Endpoints.Patients.Faces.Post;

/// <summary>A face newly enrolled for a patient.</summary>
public sealed record Response(Guid FaceId, Guid PatientId, FaceDto Face, int FacesDetected);

using SaBai.API.FaceRecognition;

namespace SaBai.API.Endpoints.Patients.Faces.Get;

/// <summary>Faces enrolled for a patient.</summary>
public sealed record Response(Guid PatientId, StoredFace[] Faces);

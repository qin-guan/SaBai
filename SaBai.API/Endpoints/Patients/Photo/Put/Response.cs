namespace SaBai.API.Endpoints.Patients.Photo.Put;

/// <summary>Result of setting a patient photo.</summary>
public sealed record Response(int Id, bool HasPhoto);

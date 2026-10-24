using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Vision.Get;

/// <summary>Patient, prescribed glasses, and acuity for a visit.</summary>
public sealed record Response(
    PatientResponse Patient,
    GlassesResponse? Vision,
    AcuityResponse? Vitals);

/// <summary>Patient identity shown with a vision record.</summary>
public sealed record PatientResponse(
    int Id,
    string Name,
    string DisplayId,
    Gender Gender,
    DateTimeOffset DateOfBirth);

/// <summary>Prescribed glasses degrees for a visit.</summary>
public sealed record GlassesResponse(
    int Id,
    int VisitId,
    string? LeftGlassesDegree,
    string? RightGlassesDegree,
    string? Notes);

/// <summary>Unaided acuity and pinhole readings for a visit.</summary>
public sealed record AcuityResponse(
    string? LeftEyeDegree,
    string? RightEyeDegree,
    string? LeftEyePinhole,
    string? RightEyePinhole);

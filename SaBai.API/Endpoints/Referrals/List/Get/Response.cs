using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Referrals.List.Get;

/// <summary>
/// Referral with the consult date, doctor, patient, and the village of the visit it was made at.
/// </summary>
public sealed record Response(
    int Id,
    string ReferredFor,
    string? ReferralNotes,
    ReferralState ReferralState,
    string? ReferralOutcome,
    int ConsultId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ConsultDate,
    Guid DoctorId,
    int PatientId,
    string PatientName,
    bool HasPhoto,
    int VillageCodeId,
    string VillageCode,
    string VillageColorHex
);
